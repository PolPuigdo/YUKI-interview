# References and Source Basis

This repository is a synthetic demo. It must not claim knowledge of Yuki's private architecture or private data model.

## Case assignment basis

The supplied Yuki AI Engineer case says:

- keep it simple;
- product judgment matters as much as architecture;
- the entrepreneur should receive a grounded, trustworthy answer from their own structured data;
- retrieval/agents/orchestration/MCP are available tools, not mandatory ingredients;
- the system should explain how grounding, trust, latency and production-quality regressions are handled;
- explicit assumptions/uncertainty are part of the evaluation.

The project therefore deliberately chooses the smallest bounded implementation that demonstrates those principles.

## Presentation decisions reflected in this repo

The four supplied presentation screenshots establish these V1 ideas:

- “V1: only three jobs”.
- Read-only.
- Verifiable.
- One market first.
- Entrepreneur.
- The LLM handles language; Yuki owns every fact.
- Auth/scope are deterministic/server-owned.
- LLM #1 is a structured router.
- A deterministic orchestrator executes allow-listed tools.
- Status, VAT/Attention and Financial Metrics are deterministic tools.
- Yuki/domain services/SQL are the source of truth.
- Evidence contains facts, IDs, freshness and links/source references.
- LLM #2 answer composition is optional.
- Claim validation/rendering is deterministic.
- Safe exits include clarification/refusal and missing/stale-data limitations.
- Trust, offline quality, production control and product outcomes are separate concerns.
- Vector search, MCP and workflow frameworks are deferred until the product earns the complexity.

## Technology checks made when these docs were authored

Date: 2026-08-30.

The implementation plan assumes:

- .NET 10 LTS is the current active LTS generation.
- PostgreSQL 18 is a stable released major version.
- Ollama exposes OpenAI-compatible chat completions.
- Ollama supports structured outputs.
- MLX-LM exposes a local HTTP server similar to the OpenAI chat API.
- `mlx-community/Qwen3-4B-Instruct-2507-4bit` is documented for use with `mlx_lm.server`.
- `qwen3.5:4b` is available in Ollama and is small enough for the narrow routing use case.

Exact patch versions should be checked at implementation time rather than copied from an old tutorial.

## Important distinction

These technical references support local runtime choices only.

They do **not** prove:

- Yuki uses PostgreSQL;
- Yuki uses these LLMs;
- Yuki's internal services have the same schema;
- “processed/booked” has the exact demo semantics;
- the dummy VAT model matches real tax rules.

Those are explicitly synthetic assumptions for the interview demo.
