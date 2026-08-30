# Architecture and Product Decisions

This file records decisions that Codex should not revisit casually.

---

## D001 — Keep the product scope to three jobs

**Decision:** V1 supports only period status, VAT attention, and current-quarter supplier spend.

**Why:** The case explicitly asks for the thinnest version worth shipping. The presentation converged on “V1: only three jobs”.

**Rejected:** “ask anything”, generic finance chat, actions, tax advice.

---

## D002 — Read-only and server-owned scope

**Decision:** The demo has one fixed synthetic tenant/administration owned by server configuration.

**Why:** The architecture principle is that Yuki authorizes and owns scope. The LLM must never choose tenant IDs.

**Rejected:** user/model-provided administration IDs; fake production auth.

---

## D003 — One ASP.NET Core 10 application for the demo

**Decision:** Use a single ASP.NET Core 10 Minimal API app for static UI, assistant API, orchestration, tools and model adapter.

**Why:** A separate Python service adds deployment/runtime complexity without adding value to a three-intent localhost demo.

**Production note:** The presentation's “C#/.NET product integration + Python AI service/evals” remains a reasonable production boundary after validation. The demo intentionally does not pre-pay that complexity.

---

## D004 — Static frontend, no React/Vue/etc.

**Decision:** Serve HTML/CSS/vanilla JS from ASP.NET Core.

**Why:** The frontend only needs a chat, example questions, loading/errors and evidence metadata.

**Rejected:** Node/npm build pipeline and SPA framework for V1.

---

## D005 — PostgreSQL 18 as synthetic SQL source of truth

**Decision:** Use PostgreSQL 18 in Docker.

**Why:** It is current, multi-platform, easy to seed and sufficient to demonstrate SQL-backed deterministic retrieval.

**Rejected:** reproducing Yuki's private datastore; vector DB; an embedded fake in-memory store that would weaken the retrieval demo.

---

## D006 — Local LLM is outside Docker

**Decision:** Ollama/MLX runs on the host; app/database run in Docker.

**Why:** Ollama benefits from native hardware and MLX targets Apple Silicon/macOS. Containerizing MLX is the wrong runtime boundary.

**Rejected:** Linux Docker container for MLX.

---

## D007 — OpenAI-compatible model adapter

**Decision:** Call `/v1/chat/completions` through plain HTTP and configuration.

**Why:** Ollama exposes OpenAI-compatible chat completions; MLX-LM exposes an OpenAI-like local HTTP server. One adapter keeps the demo provider-swappable.

**Rejected:** two provider SDK integrations unless needed later.

---

## D008 — Model only performs structured routing

**Decision:** The LLM returns a typed intent/period/confidence object.

**Why:** Language understanding benefits from a model; financial truth does not.

**Rejected:** LLM-generated SQL, LLM arithmetic, open-ended answer generation.

---

## D009 — No second LLM answer composer in core V1

**Decision:** Build the final answer from deterministic templates.

**Why:** The architecture slide explicitly marks LLM #2 as optional. Three stable jobs do not justify another inference step.

**Benefit:** lower latency, exact values, simpler validation, easier Ollama/MLX portability.

---

## D010 — Default local models are small and configurable

**Decision:**

Ollama default:

```text
qwen3.5:4b
```

MLX default:

```text
mlx-community/Qwen3-4B-Instruct-2507-4bit
```

**Why:** The task is a narrow router, not general reasoning. Larger 20B–35B models add startup/memory/latency cost with little expected value for this demo.

**Rule:** The app must not depend on quirks of one exact model; model name/base URL are configuration.

---

## D011 — Deterministic allow-listed tools only

**Decision:** Three tool capabilities, each backed by documented SQL/business rules.

**Why:** Easy to test, easy to explain, bounded risk.

**Rejected:** generic `query_database`, dynamic tool registry, autonomous planning.

---

## D012 — Synthetic data is relative to current date and idempotent

**Decision:** Seed data using current month/quarter semantics and stable IDs, with upserts.

**Why:** The same three questions should still work when the demo is run later.

**Expected financial fact:** EUR 12,460.00 net across 8 processed current-quarter purchase invoices.

---

## D013 — Evidence IDs + freshness instead of fake deep-linked product pages

**Decision:** Return source record IDs and timestamps in the demo UI.

**Why:** The real product should deep-link to Yuki records, but this repo does not contain Yuki. Building fake accounting screens would be scope creep.

---

## D014 — No RAG/vector search

**Decision:** Structured SQL retrieval only.

**Why:** All three V1 jobs depend on authoritative structured states/aggregations. Embeddings would be a worse truth source.

**Future trigger:** unstructured documents, support content, notes or policies become part of the supported use case.

---

## D015 — No MCP

**Decision:** No MCP server/client in V1.

**Why:** There is one local product experience and three internal capabilities.

**Future trigger:** the same Yuki tools need to be reused by multiple AI clients/experiences.

---

## D016 — No agent/workflow framework

**Decision:** Use normal deterministic application flow.

**Why:** One router call -> one tool -> one renderer does not require an agent graph.

**Future trigger:** genuine multi-step workflows, branching tasks, retries with state, long-running execution or human approval.

---

## D017 — Testing does not depend on a real LLM

**Decision:** Inject/fake the router for normal automated tests; run a real-local-model routing eval separately.

**Why:** Deterministic CI/tests should be fast and reproducible while still validating the actual local model before a demo.

---

## D018 — No fake observability platform

**Decision:** Structured logs + tests + local eval only.

**Why:** Production canary/release/monitoring concepts matter to the presentation, but building MLflow/OpenTelemetry dashboards locally would not improve the three-job demo.

---

## Technology currency notes

The chosen baseline is intentionally current rather than legacy:

- .NET 10 is an LTS release.
- PostgreSQL 18 is a stable major release.
- Ollama provides an OpenAI-compatible local chat endpoint and structured-output support.
- MLX-LM provides a local OpenAI-like HTTP server; its own documentation cautions against treating that basic server as a production service, which is acceptable because this project is a localhost demo.

Re-check exact patch versions when implementation begins; do not downgrade to an older major release for tutorial convenience.
