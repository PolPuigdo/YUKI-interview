# Product Scope — Yuki Assistant V1

## Product thesis

The problem is not missing accounting data. The problem is that entrepreneurs often need product/accounting knowledge to find an answer that Yuki already knows.

The V1 tests a narrow conversational access layer over authoritative structured facts.

> The LLM handles language. Yuki owns every fact.

This demo is based on the case requirement to keep the first version thin, grounded, trustworthy and useful rather than impressive-but-broad.

## Target user

**Entrepreneur**, not accountant.

For the demo:

- one synthetic user;
- one synthetic administration;
- market fixed to `NL`;
- currency fixed to `EUR`;
- no user/account switcher.

The real production product would preserve Yuki's existing role/domain/administration authorization. The demo does not invent a production auth system.

## V1 characteristics

- **Read-only**
- **Verifiable**
- **One market first**
- **Entrepreneur**
- **Three jobs only**

## Supported job 1 — Status

Canonical question:

> “Has last month been processed?”

Goal:

Return the authoritative previous-calendar-month processing state.

The assistant must not infer “processed” from prose. The status comes from the synthetic `accounting_periods` source-of-truth table.

Expected behavior:

- resolve `last month` deterministically on the server;
- retrieve the canonical period status;
- render the status + period + freshness;
- show source record ID.

If the data is missing, explain that the status cannot be determined.

## Supported job 2 — VAT attention

Canonical question:

> “What am I still missing for VAT?”

Goal:

Summarize the current demo VAT period, its status and unresolved attention items.

For V1, attention items may be:

- missing purchase invoice;
- missing sales invoice;
- open accountant question.

Expected behavior:

- resolve the current VAT period deterministically;
- retrieve canonical VAT status/deadline;
- retrieve unresolved items;
- group/count them;
- return evidence IDs and freshness.

The assistant does **not** provide VAT/tax advice.

## Supported job 3 — Financial

Canonical question:

> “How much did I spend on suppliers this quarter?”

Goal:

Return a canonical supplier-spend metric backed by exact filtered purchase-invoice records.

V1 canonical definition:

> Supplier spend = sum of `net_amount` excluding VAT for `PROCESSED` purchase invoices whose invoice date falls in the current calendar quarter for the active demo administration.

Expected seed result:

- **EUR 12,460.00**
- **8 processed purchase invoices**

The LLM must never sum the invoices.

## Supported paraphrases

The router should handle reasonable wording variants, e.g.:

- “Did my accountant finish last month?”
- “Is last month's bookkeeping processed?”
- “What is missing for my VAT return?”
- “Do I still need to send anything for VAT?”
- “What have I spent with suppliers this quarter?”
- “Supplier costs this quarter?”

Paraphrase support comes from the LLM router, not from maintaining an ever-growing keyword switch.

## Explicitly not V1

Do not implement:

- actions/write operations;
- invoice upload;
- tax advice;
- accounting advice;
- “ask anything” behavior;
- arbitrary financial analytics;
- arbitrary date-range analytics beyond the contract;
- multi-turn planning;
- autonomous agents;
- multi-agent systems;
- RAG/vector search;
- document embeddings;
- external knowledge/web search;
- MCP;
- long-term memory;
- cross-company queries;
- accountant persona/workflows;
- production identity/authentication;
- notifications;
- proactive dashboard cards;
- full Yuki navigation;
- fake deep-linked accounting screens;
- persistent thumbs-up/down feedback.

These may be valid future product ideas. They are intentionally excluded from this technical demo.

## UX contract

The chat should communicate its narrow scope.

Good:

```text
Ask about:
- last month's processing status
- what is missing for VAT
- supplier spend this quarter
```

Bad:

```text
Ask anything about your business
```

Every successful answer should contain:

1. concise answer;
2. enough context to understand the metric/state;
3. freshness;
4. evidence/source summary.

## Safe outcomes

A request can end in one of four ways:

1. **Supported and grounded** — execute tool and answer.
2. **Clarify** — low confidence / ambiguity; ask one concise clarification.
3. **Unsupported** — explain the three things the demo can answer.
4. **System failure** — state that data/model could not be reached; never fabricate.

## Demo success criteria

The technical demo succeeds when:

- all three canonical questions work;
- representative paraphrases route correctly;
- answers change when source data changes;
- exact money values match SQL;
- the user can see source/freshness metadata;
- unrelated questions do not trigger a data tool;
- the UI feels responsive enough for a live walkthrough;
- the whole environment starts/stops reproducibly.

## Product success criteria if this moved beyond demo

The real product should ultimately measure resolution, not chat volume:

- successful self-service resolution;
- time-to-answer;
- navigation reduction;
- accountant-contact reduction;
- repeat use;
- explicit user feedback as a secondary signal.

Those production metrics are conceptually important but are not required to be fully implemented in this local V1.

## Critical assumptions retained from the presentation

1. Supported entrepreneur questions are frequent and painful enough to matter.
2. “Processed/booked” can map to an authoritative Yuki state.
3. Internal read APIs/domain models can expose authoritative facts in production.
4. Canonical existing calculations should be reused rather than rebuilt by an LLM.
5. A production provider/runtime must satisfy Yuki privacy requirements.
6. One market should be validated before scaling.

The demo simulates these assumptions; it does not claim to prove Yuki's private production model.
