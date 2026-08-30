# AGENTS.md — Yuki Assistant V1

This file is the highest-priority repository guidance for Codex.

## Mission

Build the smallest completely functional local demo that proves the following architecture:

> The LLM understands the user's language and selects a supported intent. Deterministic application code authorizes scope, queries SQL, computes facts, validates them and renders the answer.

The demo must answer these three jobs:

1. `Has last month been processed?`
2. `What am I still missing for VAT?`
3. `How much did I spend on suppliers this quarter?`

Paraphrases should normally work because routing is LLM-based. The implementation must not be a hard-coded exact-string switch disguised as AI.

## Read before changing code

Before implementing a milestone, read:

- `ROADMAP.md`
- `docs/PRODUCT_SCOPE.md`
- `docs/ARCHITECTURE.md`
- `docs/DOMAIN_DATA.md`
- `docs/LLM_CONTRACT.md`
- `docs/TRUST_TESTING.md`
- `docs/RUNTIME_DEV.md`
- `docs/DECISIONS.md`

If two documents appear to conflict, prefer this order:

1. `AGENTS.md`
2. `docs/DECISIONS.md`
3. `docs/PRODUCT_SCOPE.md`
4. `docs/ARCHITECTURE.md`
5. milestone acceptance criteria in `ROADMAP.md`
6. remaining docs

Do not silently reinterpret a documented decision. If a requested change conflicts with `docs/DECISIONS.md`, **call out the conflict explicitly instead of silently changing the architecture**. If the decision genuinely needs to change, update `docs/DECISIONS.md` in the same change and explain why.

## Documentation and repository hygiene

Keep the repository documentation synchronized with the implementation.

- **Update docs whenever behavior, a public contract, runtime instructions, constraints, or an architectural decision changes.** Do not leave documentation describing behavior that the code no longer implements.
- If an architectural decision changes, update `docs/DECISIONS.md` in the same change.
- If a requested change conflicts with `docs/DECISIONS.md`, call it out before changing the architecture; do not silently override the recorded decision.
- **Commit progress in sensible, coherent units.** A commit should represent a meaningful completed piece of work rather than an arbitrary dump of unrelated changes.
- **Update `.gitignore` whenever new generated, local-only, secret, build, runtime, model-cache, IDE, or temporary files/directories are introduced.** Never commit secrets or machine-specific runtime artifacts.
- Keep commits focused on the current roadmap milestone; do not mix unrelated future work into the same commit.

## Scope invariants

These are non-negotiable for V1:

- **Entrepreneur only.**
- **NL demo market only.**
- **Read-only.**
- **Exactly three supported intent families.**
- **One fixed synthetic tenant/administration scope owned by the server.**
- **No tenant/domain/administration IDs may be accepted from the model or normal chat input.**
- **All financial/status facts come from deterministic SQL-backed code.**
- **The LLM must never calculate money, infer database truth, or generate SQL.**
- **Unsupported or uncertain requests clarify/refuse safely.**
- **No writes to accounting data.**
- **No tax advice.**
- **No accounting advice.**
- **No external web retrieval.**
- **No RAG/vector database.**
- **No autonomous agent loop.**
- **No multi-agent system.**
- **No MCP.**
- **No workflow/orchestration framework.**
- **No long-term conversation memory.**
- **No cloud LLM dependency.**
- **No MLflow/Databricks for this demo.**
- **No fake production auth system.**
- **No second microservice solely to mimic a future production architecture.**

If a proposed change is not necessary for one of the three questions, startup, grounding, safety, tests or the chat UI, do not add it.

## Technology constraints

Use:

- **.NET 10 LTS / ASP.NET Core Minimal API** for the single application backend.
- **Static HTML/CSS/vanilla JavaScript** served by ASP.NET Core for the chat UI.
- **PostgreSQL 18** for dummy structured data.
- **Npgsql** with parameterized SQL; no ORM is required.
- Built-in `HttpClient` + `System.Text.Json` for the OpenAI-compatible model API.
- Docker Compose for the app/database/bootstrap services.
- Local model endpoint on the host:
  - Ollama, or
  - MLX-LM on Apple Silicon.

Avoid adding frontend frameworks, MediatR, Dapper, EF Core, Semantic Kernel, LangChain/LangGraph or equivalent unless the existing requirements become impossible without them. They should not be necessary.

## Architectural invariants

The core request flow is:

```text
chat message
  -> server-owned scope
  -> LLM structured router
  -> route validation + confidence check
  -> deterministic allow-listed tool
  -> PostgreSQL / canonical rule
  -> evidence bundle
  -> deterministic answer renderer
  -> grounded response UI
```

The LLM is **not** the answer source.

V1 should normally require one model call per supported user question. A second answer-generation call is intentionally omitted.

## LLM behavior

The model is used only to return a typed routing decision.

The allowed intents are defined in `docs/LLM_CONTRACT.md`.

Rules:

- temperature should default to `0`;
- output must be machine-validated;
- never trust model-provided IDs;
- reject unknown intents;
- validate enum arguments;
- low confidence must not execute a tool;
- malformed output may get at most one bounded repair attempt;
- if routing still fails, return a safe assistant error;
- do not silently fall back to exact-string business logic in production code.

Tests may inject a fake router so the test suite does not require a real local LLM.

## Data rules

The database is synthetic.

Seed/bootstrap must be idempotent and must create data relative to the current date so:

- `last month` always exists;
- a current VAT period always exists;
- `this quarter` always contains the intended purchase invoices.

Canonical rules are in `docs/DOMAIN_DATA.md`.

Do not duplicate financial calculations in prompts or in the frontend.

## Response rules

Every successful answer must expose:

- the rendered answer;
- intent;
- freshness timestamp;
- human-readable evidence summary;
- source record identifiers sufficient to prove where the value came from.

Because this is not the real Yuki product, fake deep-link pages are not required. Do not build a second mini accounting UI just to have clickable links.

## Error / safe-exit rules

Handle explicitly:

- unsupported question;
- ambiguous/low-confidence question;
- malformed LLM routing response;
- unavailable LLM endpoint;
- database unavailable;
- missing expected data;
- stale/incomplete data condition if represented by the tool;
- invalid tool arguments.

Never invent an answer to keep the chat flowing.

## Testing rules

Every milestone that introduces logic must add or update tests.

At minimum the final project must have:

- unit tests for relative-period resolution;
- unit tests for router-output validation;
- unit tests for each deterministic tool;
- exact numerical test for supplier spend;
- tests that server-owned scope cannot be overridden;
- renderer tests;
- integration test against PostgreSQL/bootstrap;
- API tests using a fake LLM router;
- optional local real-model eval/smoke test that is not required by normal CI.

Run the smallest relevant test set during a milestone, then the full suite before marking it complete.

## PowerShell / Bash parity

`start.ps1` and `start.sh` must have equivalent behavior.

`stop.ps1` and `stop.sh` must have equivalent behavior.

Do not add a feature to only one platform script.

## Lifecycle-script safety

The scripts may start a local LLM process if configured to do so.

They must **not kill an Ollama/MLX process that was already running before the project started**.

If a script launches an LLM process itself, it may record its PID under `.runtime/` and only stop that recorded process.

`stop.*` must not delete the PostgreSQL volume by default.

## Milestone discipline

Work on one `ROADMAP.md` milestone at a time.

When asked to `/plan` a milestone:

1. inspect the current repository;
2. identify only the files needed for that milestone;
3. preserve all invariants above;
4. state tests/acceptance checks;
5. do not pre-implement later milestones.

When a milestone is complete and verified, **mark it as completed in the roadmap that owns it** and update its status/evidence in `ROADMAP.md` before considering the work finished.

## Do not overengineer

**Do not overengineer this V1.** Prefer the smallest direct implementation that satisfies the current milestone and documented acceptance criteria. Do not add abstractions, infrastructure, extensibility, services, frameworks, generic platforms, configuration layers or future-proofing unless the current V1 demonstrably needs them.

When two designs satisfy the requirement equally well, choose the simpler one with fewer moving parts.

## Definition of a bad change

Reject or simplify a proposal if it:

- adds a service that can be a class/module;
- adds an abstraction with only one implementation and no current need;
- adds agentic iteration for a deterministic three-intent flow;
- stores embeddings for structured data;
- lets the LLM own authorization or business truth;
- adds hidden fallback behavior that makes a demo look successful without actually using the intended pipeline;
- optimizes for hypothetical future requirements instead of the V1.

The best implementation is the smallest one that makes the three flows real, grounded, reproducible and easy to explain.
