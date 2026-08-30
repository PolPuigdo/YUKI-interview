# Local Runtime and Developer Experience

## Objective

The repository should be runnable as a small local product, not as a collection of manual setup steps.

Required lifecycle entrypoints:

```text
start.ps1
start.sh
stop.ps1
stop.sh
```

PowerShell and Bash must be behaviorally equivalent.

## Runtime topology

### Dockerized

- `app` — ASP.NET Core 10 API + static frontend.
- `db` — PostgreSQL 18.
- `db-init` — optional one-shot service that applies idempotent schema/seed after DB health is green.

### Host runtime

Exactly one configured local LLM server:

- Ollama, or
- MLX-LM.

Do not run MLX in Docker.

## Ports

Use non-conflicting defaults:

```text
App:        http://localhost:8088
Postgres:   internal to Compose by default
Ollama:     http://localhost:11434
MLX-LM:     http://localhost:8080
```

The app container reaches host runtimes through `host.docker.internal`. Compose includes the standard `host-gateway` mapping so this also works on Linux Docker Engine; Docker Desktop supports the same mapping.

## `.env.example`

The final project should document at least:

```text
# App
ASPNETCORE_ENVIRONMENT=Development
APP_PORT=8088

# Demo scope
DEMO_TENANT_ID=demo-tenant
DEMO_ADMINISTRATION_ID=northstar-bikes-nl
DEMO_MARKET=NL
DEMO_CURRENCY=EUR

# Database
POSTGRES_DB=yuki_demo
POSTGRES_USER=yuki
POSTGRES_PASSWORD=yuki_local_only
ConnectionStrings__YukiDemo=Host=db;Port=5432;Database=yuki_demo;Username=yuki;Password=yuki_local_only

# LLM
LLM_PROVIDER=ollama
LLM_BASE_URL=http://host.docker.internal:11434/v1
LLM_MODEL=qwen3.5:4b
LLM_API_KEY=local-not-used
LLM_TIMEOUT_SECONDS=60
ROUTER_CONFIDENCE_THRESHOLD=0.70
LLM_AUTOSTART=false
```

Secrets are not important for synthetic local data, but do not commit a real credential.

## `start.*` contract

Both scripts should perform these logical steps.

### 1. Validate Docker

Fail early with a useful message if Docker/Compose is unavailable.

### 2. Load configuration

Use `.env` when present; otherwise use/document `.env.example` defaults where safe.

Do not silently guess an MLX model on non-Apple hardware.

### 3. Resolve the LLM provider

Supported:

```text
ollama
mlx
```

Reject unknown provider values.

### 4. Check LLM endpoint

If the configured endpoint is already healthy:

- reuse it;
- record that the project does not own that process.
- verify that the configured Ollama model is installed; if it is missing, fail with the exact `ollama pull` command unless autostart is enabled.

If it is not healthy and `LLM_AUTOSTART=false`:

- fail with the exact command needed to start it.

If `LLM_AUTOSTART=true`:

#### Ollama

- confirm `ollama` CLI exists;
- ensure configured model exists/pull if necessary;
- start `ollama serve` only if no server is already available;
- record PID only if this script created the process.

Be careful on installations where Ollama already runs as an app/service.

#### MLX

Supported only where `mlx_lm.server` is available.

- confirm command exists;
- launch configured model with explicit host/port;
- record PID under `.runtime/llm.pid`;
- wait for endpoint health.

Default MLX model:

```text
mlx-community/Qwen3-4B-Instruct-2507-4bit
```

### 5. Start Docker

Conceptually:

```text
docker compose up -d --build
```

The database must become healthy before bootstrap/app readiness.

### 6. Bootstrap SQL

Run idempotent schema + seed every startup, either:

- via a one-shot `db-init` Compose service; or
- via an explicit startup command after DB health.

Prefer the approach with the least script duplication.

The same SQL must be used on PowerShell and Bash.

### 7. Wait for app health

Poll:

```text
GET http://localhost:8088/health
```

Compose also declares an application health check against the same endpoint. The
application health check confirms that the web process is serving requests; it
does not make the LLM a startup dependency.

Optionally expose an LLM dependency status separately.

### 8. Print concise status

Example:

```text
Yuki Assistant V1 is ready
App:      http://localhost:8088
LLM:      ollama / qwen3.5:4b
Database: PostgreSQL 18 / healthy
```

Do not automatically open the browser unless explicitly desired.

## `stop.*` contract

Both scripts should:

1. `docker compose down`;
2. preserve named DB volume;
3. check `.runtime/llm.pid`;
4. stop the recorded process only if it is still the process owned by this project;
5. remove stale PID metadata.

They must not indiscriminately kill all `ollama` or Python processes.

Both start scripts validate any existing PID metadata against the recorded
provider and process command line before retaining it. Stop scripts apply the
same ownership check before sending a termination signal.

For a full manual reset, document a separate command such as:

```text
docker compose down -v
```

Do not make destructive reset the default stop behavior.

## Health endpoints

Minimum:

```text
GET /health
```

Return healthy only when the app process is ready.

Useful optional endpoint:

```text
GET /api/health/dependencies
```

Can report:

- DB connectivity;
- LLM endpoint connectivity;
- configured provider/model.

Do not expose secrets.

## Project structure guidance

Inside the single ASP.NET Core project, prefer simple feature folders:

```text
Assistant/
  AssistantEndpoint.cs
  AssistantOrchestrator.cs
  Routing/
    ILlmRouter.cs
    OpenAiCompatibleLlmRouter.cs
    RouterResult.cs
    RouterValidator.cs
  Tools/
    PeriodStatusTool.cs
    VatAttentionTool.cs
    SupplierSpendTool.cs
  Rendering/
    EvidenceBundle.cs
    GroundedAnswerRenderer.cs

Data/
  DemoScope.cs
  NpgsqlConnectionFactory.cs
  DatePeriodResolver.cs

wwwroot/
  index.html
  app.js
  styles.css
```

Names may change slightly if implementation clarity improves.

Do not create separate class-library projects for each folder.

## Local development outside Docker

Docker is the primary demo path.

It is acceptable to support:

```text
dotnet run
```

for faster coding, pointing at a locally exposed PostgreSQL and host model.

Do not make local non-Docker execution more complex than Compose.

## Logging

Console structured logging is enough.

Per chat request log:

```text
correlation_id
intent
confidence
llm_ms
tool_name
tool_ms
source_count
total_ms
safe_exit_reason
```

Avoid logging whole prompts/results at high verbosity by default.

## Cross-platform expectations

### Windows

Primary shell:

```text
PowerShell 7+
```

Ollama is the likely local provider.

### macOS Apple Silicon

Bash/zsh-compatible `start.sh`.

Either:

- Ollama; or
- MLX-LM.

### Linux

Bash path should work with Ollama.

MLX is not expected on Linux.

## Version policy

Do not pin obsolete major versions.

Use:

- .NET 10 LTS;
- PostgreSQL 18;
- current stable Docker Compose;
- current stable Ollama;
- current stable MLX-LM compatible with the configured MLX model.

Patch/minor pins may be placed in Dockerfiles/lock files for reproducibility, but the docs should not force old patches.
