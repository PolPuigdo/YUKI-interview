#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$project_root"
if command -v docker >/dev/null 2>&1; then docker compose down; fi

pid_path="$project_root/.runtime/llm.pid"
owner_path="$project_root/.runtime/llm.owner"
if [[ -f "$pid_path" ]]; then
  tracked_pid="$(head -n 1 "$pid_path" || true)"
  owner="$(head -n 1 "$owner_path" 2>/dev/null || true)"
  if [[ "$tracked_pid" =~ ^[0-9]+$ ]] && kill -0 "$tracked_pid" >/dev/null 2>&1; then
    command_line="$(ps -p "$tracked_pid" -o args= 2>/dev/null || true)"
    if { [[ "$owner" == ollama && "$command_line" == *'ollama serve'* ]]; } || { [[ "$owner" == mlx && "$command_line" == *'mlx_lm.server'* ]]; }; then
      kill "$tracked_pid" >/dev/null 2>&1 || true
    fi
  fi
  rm -f "$pid_path" "$owner_path"
fi
