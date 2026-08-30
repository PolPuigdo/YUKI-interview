# Domain and Dummy Data Contract

## Purpose

This file defines the entire synthetic source of truth for V1.

The schema should be only large enough to answer the three supported jobs.

## Demo scope

Use a fixed server-owned scope, for example:

```text
Tenant ID:         demo-tenant
Administration ID: northstar-bikes-nl
Administration:    Northstar Bikes B.V.
Market:            NL
Currency:          EUR
```

All records belong to that administration.

The browser and LLM never choose this scope.

## Required tables

Keep the schema minimal.

### `administrations`

Suggested fields:

```text
id              text/uuid primary key
tenant_id       text not null
name            text not null
market          text not null
currency        text not null
updated_at      timestamptz not null
```

### `accounting_periods`

Purpose: authoritative answer for “Has last month been processed?”

Suggested fields:

```text
id                 text/uuid primary key
administration_id  fk not null
period_start       date not null
period_end         date not null
status             text not null
processed_through  date null
updated_at         timestamptz not null
```

Allowed V1 statuses:

```text
OPEN
PROCESSING
PROCESSED
```

Seed rule:

- create the previous calendar month;
- status = `PROCESSED`;
- `processed_through` = final date of previous month.

Do not derive status from invoice counts.

### `vat_periods`

Purpose: current VAT state.

Suggested fields:

```text
id                 text/uuid primary key
administration_id  fk not null
period_start       date not null
period_end         date not null
deadline           date not null
status             text not null
updated_at         timestamptz not null
```

Allowed V1 statuses:

```text
DRAFT
READY
SUBMITTED
```

Seed rule:

- current calendar quarter is the current demo VAT period;
- status = `DRAFT`;
- deadline is a synthetic/demo deadline derived from the period;
- it is not legal/tax advice.

### `vat_attention_items`

Purpose: deterministic unresolved VAT-related attention items.

Suggested fields:

```text
id                 text/uuid primary key
vat_period_id      fk not null
administration_id  fk not null
item_type          text not null
label              text not null
resolved           boolean not null
source_ref         text not null
updated_at         timestamptz not null
```

Allowed V1 item types:

```text
MISSING_PURCHASE_INVOICE
MISSING_SALES_INVOICE
OPEN_QUESTION
```

Seed exactly these unresolved logical items:

- 2 × missing purchase invoice;
- 1 × missing sales invoice;
- 1 × open accountant question.

The VAT answer should therefore report **4 unresolved attention items**.

### `purchase_invoices`

Purpose: authoritative records for supplier spend.

Suggested fields:

```text
id                 text/uuid primary key
administration_id  fk not null
supplier_name      text not null
invoice_number     text not null
invoice_date       date not null
net_amount         numeric(18,2) not null
vat_amount         numeric(18,2) not null
status             text not null
updated_at         timestamptz not null
```

Allowed V1 statuses:

```text
DRAFT
PROCESSED
```

## Canonical relative-period rules

Resolve time in deterministic application code, using the server date.

### `last_month`

Previous calendar month.

Example if today is 2026-08-30:

```text
start = 2026-07-01
end   = 2026-07-31
```

### `current_quarter`

Current calendar quarter.

Example if today is 2026-08-30:

```text
start = 2026-07-01
end   = 2026-09-30
```

### `current_vat_period`

For the demo, use the seeded VAT period that contains today's date.

No LLM should calculate these dates.

## Canonical tool rules

## 1. Period processing status

Input:

```text
scope = server-owned
period = last_month
```

Query the exact matching `accounting_periods` record.

Return structured facts such as:

```text
period_start
period_end
status
processed_through
updated_at
source_id
```

The tool must not reinterpret `PROCESSED`.

## 2. VAT attention

Input:

```text
scope = server-owned
period = current_vat_period
```

Return:

```text
VAT status
deadline
unresolved item count
counts by item type
individual item labels/IDs
freshness
```

Only `resolved = false` items count as missing/attention.

The tool does not decide whether the business is legally ready to file VAT.

## 3. Supplier spend

V1 canonical definition:

> Sum `net_amount` excluding VAT for `PROCESSED` purchase invoices in the active demo administration whose `invoice_date` falls inside the current calendar quarter.

Do not include:

- draft invoices;
- invoices outside the quarter;
- VAT amount;
- another administration.

Seed 8 processed current-quarter invoices with these net amounts:

```text
EUR 1,250.00
EUR   980.00
EUR 2,400.00
EUR   675.50
EUR 3,100.00
EUR 1,499.50
EUR 1,275.00
EUR 1,280.00
----------------
EUR 12,460.00
```

Also seed at least:

- one draft current-quarter invoice that must not count;
- one processed previous-quarter invoice that must not count.

This makes the aggregation test meaningful.

Expected canonical result:

```text
net_amount = 12460.00
currency = EUR
invoice_count = 8
```

## Idempotent seed strategy

`002_seed.sql` must be safe to run repeatedly.

Use stable synthetic IDs and `INSERT ... ON CONFLICT ... DO UPDATE` or equivalent.

Seed dates should be calculated relative to `CURRENT_DATE`, not hard-coded to August 2026.

Rerunning bootstrap should restore canonical demo values so the interview environment is reproducible.

## Freshness

Each source record has `updated_at`.

A tool result's freshness should be the most relevant/max `updated_at` used to answer it.

The UI can display a simple timestamp such as:

```text
Data updated: 2026-08-30 14:20
```

## Evidence source IDs

Return record IDs, not entire raw rows.

Examples:

```text
accounting-period-previous-month
vat-period-current
vat-attention-purchase-01
purchase-invoice-q-current-01
```

Record IDs make the grounding visible while keeping the API response compact.

## Deliberate simplifications

The dummy model does not attempt to reproduce:

- Yuki's private/internal schema;
- ledger postings;
- chart of accounts;
- supplier master-data logic;
- country-specific tax legislation;
- multi-currency accounting;
- corrections/credit notes;
- accrual accounting;
- real “booked” semantics.

Those belong to real domain APIs in a production implementation. The demo only proves the system boundary: authoritative deterministic domain data vs language model.
