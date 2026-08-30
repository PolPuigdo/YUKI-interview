#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$project_root"

if [[ -f .env ]]; then
  while IFS= read -r env_line || [[ -n "$env_line" ]]; do
    env_line="${env_line#"${env_line%%[![:space:]]*}"}"
    [[ -z "$env_line" || "${env_line:0:1}" == '#' ]] && continue
    [[ "$env_line" != *=* ]] && continue
    env_name="${env_line%%=*}"
    env_value="${env_line#*=}"
    env_name="${env_name%"${env_name##*[![:space:]]}"}"
    env_value="${env_value#"${env_value%%[![:space:]]*}"}"
    env_value="${env_value%"${env_value##*[![:space:]]}"}"
    if [[ "$env_value" == '"'*'"' || "$env_value" == "'"*"'" ]]; then
      env_value="${env_value:1:${#env_value}-2}"
    fi
    export "$env_name=$env_value"
  done < .env
fi

: "${APP_PORT:=8088}"
: "${POSTGRES_DB:=yuki_demo}"
: "${POSTGRES_USER:=yuki}"
: "${POSTGRES_PASSWORD:=yuki_local_only}"
: "${LLM_PROVIDER:=ollama}"
: "${LLM_AUTOSTART:=false}"

command -v docker >/dev/null 2>&1 || { echo 'Docker CLI is required. Install Docker Desktop and try again.' >&2; exit 1; }
command -v curl >/dev/null 2>&1 || { echo 'curl is required to check local services.' >&2; exit 1; }
docker info >/dev/null 2>&1 || { echo 'Docker Engine is not available. Start Docker Desktop and try again.' >&2; exit 1; }
docker compose version >/dev/null 2>&1 || { echo 'Docker Compose is required.' >&2; exit 1; }

provider="${LLM_PROVIDER,,}"
case "$provider" in ollama|mlx) ;; *) echo "Unsupported LLM_PROVIDER '$provider'. Use ollama or mlx." >&2; exit 1 ;; esac
if [[ "$provider" == mlx ]]; then
  : "${LLM_BASE_URL:=http://host.docker.internal:8080/v1}"
  : "${LLM_MODEL:=mlx-community/Qwen3-4B-Instruct-2507-4bit}"
else
  : "${LLM_BASE_URL:=http://host.docker.internal:11434/v1}"
  : "${LLM_MODEL:=qwen3.5:4b}"
fi
export APP_PORT POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD LLM_PROVIDER LLM_BASE_URL LLM_MODEL LLM_AUTOSTART
runtime_dir="$project_root/.runtime"
mkdir -p "$runtime_dir"
pid_path="$runtime_dir/llm.pid"
owner_path="$runtime_dir/llm.owner"
log_path="$runtime_dir/llm.log"

endpoint_ready() {
  local check_base="${LLM_BASE_URL//host.docker.internal/localhost}"
  curl -fsS --max-time 3 "${check_base%/}/models" >/dev/null 2>&1
}

wait_endpoint() {
  local check_base="${1//host.docker.internal/localhost}"
  local description="$2"
  local endpoint_path="${3:-/models}"
  for _ in $(seq 1 30); do
    if curl -fsS --max-time 3 "${check_base%/}${endpoint_path}" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  echo "$description did not become ready in time." >&2
  exit 1
}

if ! endpoint_ready; then
  if [[ "${LLM_AUTOSTART,,}" != true ]]; then
    echo "The $provider endpoint is unavailable. Start it or set LLM_AUTOSTART=true. Expected endpoint: $LLM_BASE_URL" >&2
    exit 1
  fi
  if [[ "$provider" == ollama ]]; then
    command -v ollama >/dev/null 2>&1 || { echo 'LLM_AUTOSTART=true requires the ollama CLI.' >&2; exit 1; }
    ollama show "$LLM_MODEL" >/dev/null 2>&1 || ollama pull "$LLM_MODEL"
    ollama serve >"$log_path" 2>&1 &
    llm_pid=$!
  else
    command -v python3 >/dev/null 2>&1 || { echo 'LLM_AUTOSTART=true for mlx requires python3.' >&2; exit 1; }
    python3 -c 'import mlx_lm' >/dev/null 2>&1 || { echo 'LLM_AUTOSTART=true for mlx requires the mlx-lm package.' >&2; exit 1; }
    mlx_port="$(python3 -c 'from urllib.parse import urlparse; import os; print(urlparse(os.environ["LLM_BASE_URL"]).port or 8080)')"
    python3 -m mlx_lm.server --model "$LLM_MODEL" --host 0.0.0.0 --port "$mlx_port" >"$log_path" 2>&1 &
    llm_pid=$!
  fi
  printf '%s\n' "$llm_pid" >"$pid_path"
  printf '%s\n' "$provider" >"$owner_path"
  wait_endpoint "$LLM_BASE_URL" "The $provider endpoint"
elif [[ -f "$pid_path" ]]; then
  tracked_pid="$(head -n 1 "$pid_path" || true)"
  if [[ -z "$tracked_pid" || ! "$tracked_pid" =~ ^[0-9]+$ || ! -e "/proc/$tracked_pid" ]]; then
    rm -f "$pid_path" "$owner_path"
  fi
fi

if [[ "$provider" == ollama ]]; then
  command -v ollama >/dev/null 2>&1 || { echo 'The configured ollama provider requires the ollama CLI.' >&2; exit 1; }
  if ! ollama show "$LLM_MODEL" >/dev/null 2>&1; then
    if [[ "${LLM_AUTOSTART,,}" == true ]]; then
      ollama pull "$LLM_MODEL"
    else
      echo "Ollama is running, but model '$LLM_MODEL' is not installed. Run 'ollama pull $LLM_MODEL' or set LLM_AUTOSTART=true." >&2
      exit 1
    fi
  fi
fi

docker compose up -d db
for attempt in $(seq 1 30); do
  if docker compose exec -T db pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null 2>&1; then break; fi
  [[ "$attempt" -eq 30 ]] && { echo 'PostgreSQL did not become ready in time.' >&2; exit 1; }
  sleep 2
done
docker compose run --rm db-init
docker compose up -d --build app

for attempt in $(seq 1 30); do
  if curl -fsS --max-time 3 "http://localhost:$APP_PORT/health" >/dev/null 2>&1; then break; fi
  [[ "$attempt" -eq 30 ]] && { echo 'The Yuki Assistant app did not become ready in time.' >&2; exit 1; }
  sleep 2
done

echo
echo 'Yuki Assistant V1 is ready'
printf 'App:      http://localhost:%s\n' "$APP_PORT"
printf 'LLM:      %s / %s\n' "$provider" "$LLM_MODEL"
echo 'Database: PostgreSQL 18 / healthy'
