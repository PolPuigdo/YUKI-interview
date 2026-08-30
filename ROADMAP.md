# ROADMAP — Yuki Assistant V1

Implement one milestone at a time. Do not start later milestones early.

Status values: `TODO`, `IN PROGRESS`, `DONE`.

---

## M00 — Repository scaffold and executable skeleton

**Status:** DONE

### Goal

Create the smallest .NET 10 repository that builds, tests and can eventually host both the API and static chat UI.

### Scope

- Create solution and ASP.NET Core 10 Minimal API project.
- Create xUnit test project.
- Add `Npgsql` dependency only where needed.
- Add placeholder `/health` endpoint.
- Create target directories described in `README.md`.
- Add `.gitignore` and `.env.example`.
- Add empty/static `wwwroot` landing page only if useful to prove static-file serving.
- Add an initial `compose.yaml` skeleton only if required for validation; full lifecycle work belongs to M06.

### Acceptance criteria

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- App can start locally and `/health` returns success.
- No business behavior is implemented yet.
- No extra framework/service has been introduced.

### Not in this milestone

- SQL schema/seed.
- LLM.
- chat endpoint.
- final UI.
- lifecycle scripts.

### Evidence

- `dotnet build YukiAssistantDemo.slnx --no-restore` succeeds with zero warnings and errors.
- `dotnet test YukiAssistantDemo.slnx --no-restore --no-build` passes the HTTP health smoke test.
- Local app verification confirms `GET /health` returns `{ "status": "healthy" }` and the static landing page returns HTTP 200.

---

## M01 — PostgreSQL schema and deterministic dummy dataset

**Status:** TODO

### Goal

Create the synthetic source of truth required by the three V1 jobs.

### Scope

Implement:

- `db/001_schema.sql`
- `db/002_seed.sql`
- PostgreSQL container configuration.
- one-shot idempotent bootstrap/upsert behavior.
- tables and fields defined in `docs/DOMAIN_DATA.md`.
- synthetic NL administration.
- date-relative records for previous month, current VAT period and current quarter.
- stable supplier-spend seed total of **EUR 12,460.00 net** across **8 processed purchase invoices**.

### Acceptance criteria

- Fresh DB starts successfully.
- Running bootstrap more than once produces the same logical state.
- Previous calendar month exists and is `PROCESSED`.
- Current VAT period exists with the documented unresolved items.
- Current quarter has exactly the expected processed invoice set and exact EUR 12,460.00 net total.
- Data is synthetic and clearly marked as demo data.

### Tests/checks

- SQL/bootstrap smoke check.
- automated integration test may be deferred to M07, but provide a reproducible query/check now.

### Not in this milestone

- LLM.
- assistant tools.
- UI.

---

## M02 — Deterministic domain queries and three tools

**Status:** TODO

### Goal

Implement the three authoritative data capabilities without any LLM dependency.

### Scope

Implement small parameterized query functions/services for:

1. `GetPeriodProcessingStatus(last_month)`
2. `GetVatAttention(current_vat_period)`
3. `GetSupplierSpend(current_quarter)`

Also implement:

- server-owned `DemoScope`;
- relative-date resolver;
- canonical DTOs/results;
- freshness/source identifiers.

### Acceptance criteria

Given the seeded DB:

- status tool reports the previous month as `PROCESSED`;
- VAT tool reports the current VAT state and exact unresolved items;
- financial tool returns exactly EUR 12,460.00 net and 8 processed invoices;
- no tool accepts tenant/domain/administration IDs from chat/model input;
- all SQL is parameterized;
- tools do not generate prose.

### Tests

- unit tests for date resolution;
- tool tests;
- exact financial aggregation test;
- scope override tests.

### Not in this milestone

- LLM routing.
- chat endpoint.
- answer prose/UI.

---

## M03 — Local LLM adapter and structured router

**Status:** DONE

### Goal

Use a local model only to classify the user message into the allow-listed V1 contract.

### Scope

Implement:

- provider-agnostic OpenAI-compatible chat-completions client;
- environment configuration for Ollama and MLX;
- router system prompt;
- JSON router contract from `docs/LLM_CONTRACT.md`;
- schema/enum validation;
- confidence threshold;
- unsupported and clarification routing;
- at most one bounded malformed-output repair attempt;
- timeout/cancellation.

### Acceptance criteria

With a configured local model, the router correctly maps the three canonical questions to:

- `period_processing_status`
- `vat_missing_items`
- `supplier_spend`

and rejects unrelated requests.

Paraphrases should work in a small manual smoke set.

The router:

- does not return/accept tenant IDs;
- does not answer the accounting question itself;
- cannot select an arbitrary tool;
- cannot generate SQL.

### Tests

- parser/validator tests;
- fake-model response tests;
- malformed/unknown-intent tests;
- low-confidence tests.

Real-model eval is not required for ordinary unit tests.

### Evidence

- OpenAI-compatible local router implemented with configurable Ollama/MLX endpoint, model, timeout and confidence threshold.
- Application-side JSON, enum, period/confidence and extra-field validation rejects unsafe or malformed routes.
- Invalid model output receives at most one bounded repair attempt; timeouts, unavailable endpoints and repeated invalid output fail safely.
- Automated suite covers valid routes, malformed/unknown output, repair behavior, low confidence, scope-related prompt invariants and the existing health smoke test.
- `dotnet test YukiAssistantDemo.slnx --no-restore` passes all 8 tests.

### Not in this milestone

- deterministic tool execution from chat.
- frontend.

---

## M04 — Assistant pipeline, evidence bundle and grounded answers

**Status:** DONE

### Goal

Connect routing to deterministic tools and return safe, grounded responses.

### Scope

Implement:

```text
message
 -> router
 -> deterministic route validation
 -> tool execution
 -> evidence bundle
 -> deterministic answer renderer
 -> API response
```

Add:

- `POST /api/chat`
- safe exits
- evidence/freshness in response
- deterministic templates for the three supported answer families
- explicit unsupported/clarification/error responses.

### Acceptance criteria

The three supported questions return correct grounded answers using the DB facts.

The API response clearly separates:

- user-facing answer;
- intent;
- evidence;
- freshness;
- source record IDs.

The LLM is not called again to write the answer.

Changing the database fact changes the answer without changing the prompt.

If the router is uncertain, no data tool executes.

### Tests

- API tests with fake router;
- answer renderer tests;
- evidence tests;
- DB/LLM failure safe-exit tests.

### Evidence

- Implemented server-owned scope, deterministic period resolution, parameterized PostgreSQL tools, evidence bundles and deterministic rendering for all three supported intents.
- Added `POST /api/chat`; its request contains only `message`, and successful responses separate `answer`, `intent`, `evidence`, source IDs and freshness.
- Low-confidence, unsupported, malformed-router and unavailable-LLM routes execute no business tool; missing data and database failures return safe responses.
- Added renderer, date-resolution and fake-router API tests. `dotnet test YukiAssistantDemo.slnx -nologo` passes 14 tests.
- Real PostgreSQL/bootstrap verification remains an environment-dependent integration check for M07; Docker Engine was unavailable during this implementation.

---

## M05 — Minimal chat UI

**Status:** DONE

### Goal

Provide a clean, interview-ready chat surface without adding a frontend framework.

### Scope

Implement under `wwwroot/`:

- one-page chat;
- assistant/user message bubbles;
- input + send button;
- loading state;
- safe error state;
- three example-question chips/buttons;
- a compact evidence/freshness section per successful answer;
- responsive layout;
- simple white/black/blue visual language inspired by the presentation, not a clone of Yuki.

### Acceptance criteria

From the browser the user can ask all three questions and see grounded answers.

The UI:

- does not expose tenant/administration controls;
- does not claim “ask anything”;
- makes supported examples visible;
- visibly distinguishes source/evidence metadata from answer prose;
- works without a Node/npm runtime.

### Not in this milestone

- account screens;
- fake accounting dashboard;
- authentication UI;
- feedback persistence;
- elaborate animations/design system.

### Evidence

- Replaced the static landing page with a responsive vanilla HTML/CSS/JavaScript chat UI served directly by ASP.NET Core.
- The UI sends only `{ "message": "..." }` to `POST /api/chat`, exposes the three supported example questions, and renders user/assistant messages, loading state and safe network/API errors.
- Successful responses show answer prose separately from evidence summary, source IDs, freshness and the optional intent label; no tenant or administration controls are exposed.
- Added an integration smoke test proving the page, stylesheet and script are served without a Node/npm runtime.

---

## M06 — Docker Compose and cross-platform lifecycle scripts

**Status:** TODO

### Goal

Make the entire demo reproducible with one start command and one stop command on Windows and macOS/Linux.

### Scope

Create:

- `compose.yaml`
- app Dockerfile
- PostgreSQL 18 service
- one-shot DB bootstrap service if needed
- health checks
- `start.ps1`
- `start.sh`
- `stop.ps1`
- `stop.sh`
- `.runtime/` PID handling for a model process launched by the scripts.

Behavior is defined in `docs/RUNTIME_DEV.md`.

### Acceptance criteria

`start.ps1` and `start.sh`:

- validate prerequisites;
- resolve configured LLM provider;
- reuse an already-running healthy local LLM endpoint;
- optionally start the configured local model runtime when enabled;
- do not containerize MLX;
- build/start app + PostgreSQL;
- run idempotent DB bootstrap;
- wait for health;
- print the app URL and active model/provider.

`stop.ps1` and `stop.sh`:

- stop Docker services;
- stop only an LLM process that this project itself launched;
- preserve DB volume by default.

App is reachable at the documented localhost URL.

### Cross-platform requirement

PowerShell and Bash behavior must be equivalent.

---

## M07 — Trust, integration tests and local eval set

**Status:** TODO

### Goal

Prove the demo is not just a happy-path presentation.

### Scope

Complete the test/eval matrix in `docs/TRUST_TESTING.md`.

Include:

- full unit suite;
- PostgreSQL integration tests;
- API tests with fake LLM;
- prompt-injection/tenant-override tests;
- unsupported/ambiguous cases;
- optional real-local-model eval command that reports pass/fail per example and does not run in normal CI.

### Acceptance criteria

- all deterministic numerical/status tests pass exactly;
- unauthorized scope override is impossible through the chat contract;
- unsupported questions do not execute a business tool;
- missing LLM/DB produce safe visible errors;
- local real-model eval can be run manually when Ollama/MLX is available.

No LLM-as-judge is needed.

---

## M08 — End-to-end demo hardening and completion

**Status:** TODO

### Goal

Make the final V1 easy to run, explain and reset for an interview.

### Scope

- run from a clean checkout;
- verify Windows PowerShell path;
- verify Bash path;
- verify Ollama path;
- verify MLX path on Apple Silicon when available;
- tighten README quick start;
- remove dead code/dependencies;
- check logs are useful but not noisy;
- verify all three canonical questions and representative paraphrases;
- verify one unsupported and one low-confidence request;
- ensure docs match implementation.

### Final definition of done

The project is complete only when:

1. one command starts the local demo;
2. the chat opens in a browser;
3. the three jobs work from synthetic SQL facts;
4. the LLM only routes language;
5. money/status truth is deterministic;
6. evidence and freshness are visible;
7. unsupported/uncertain requests fail safely;
8. Ollama and MLX are both configurable;
9. start/stop work on `.ps1` and `.sh`;
10. full automated test suite is green;
11. there is no RAG, MCP, autonomous agent, vector DB, cloud dependency or unnecessary microservice.

After M08, stop. New product capabilities belong to a future roadmap, not V1.
