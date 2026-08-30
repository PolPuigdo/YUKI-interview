BEGIN;

-- Synthetic, read-only source of truth for the Yuki Assistant V1 demo.
CREATE TABLE IF NOT EXISTS administrations (
    id text PRIMARY KEY,
    tenant_id text NOT NULL,
    name text NOT NULL,
    market text NOT NULL,
    currency text NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS accounting_periods (
    id text PRIMARY KEY,
    administration_id text NOT NULL REFERENCES administrations(id),
    period_start date NOT NULL,
    period_end date NOT NULL,
    status text NOT NULL CHECK (status IN ('OPEN', 'PROCESSING', 'PROCESSED')),
    processed_through date,
    updated_at timestamptz NOT NULL,
    CONSTRAINT accounting_period_dates_valid CHECK (period_end >= period_start)
);

CREATE TABLE IF NOT EXISTS vat_periods (
    id text PRIMARY KEY,
    administration_id text NOT NULL REFERENCES administrations(id),
    period_start date NOT NULL,
    period_end date NOT NULL,
    deadline date NOT NULL,
    status text NOT NULL CHECK (status IN ('DRAFT', 'READY', 'SUBMITTED')),
    updated_at timestamptz NOT NULL,
    CONSTRAINT vat_period_dates_valid CHECK (period_end >= period_start)
);

CREATE TABLE IF NOT EXISTS vat_attention_items (
    id text PRIMARY KEY,
    vat_period_id text NOT NULL REFERENCES vat_periods(id),
    administration_id text NOT NULL REFERENCES administrations(id),
    item_type text NOT NULL CHECK (item_type IN (
        'MISSING_PURCHASE_INVOICE',
        'MISSING_SALES_INVOICE',
        'OPEN_QUESTION'
    )),
    label text NOT NULL,
    resolved boolean NOT NULL,
    source_ref text NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS purchase_invoices (
    id text PRIMARY KEY,
    administration_id text NOT NULL REFERENCES administrations(id),
    supplier_name text NOT NULL,
    invoice_number text NOT NULL,
    invoice_date date NOT NULL,
    net_amount numeric(18, 2) NOT NULL,
    vat_amount numeric(18, 2) NOT NULL,
    status text NOT NULL CHECK (status IN ('DRAFT', 'PROCESSED')),
    updated_at timestamptz NOT NULL,
    CONSTRAINT purchase_amounts_non_negative CHECK (net_amount >= 0 AND vat_amount >= 0)
);

CREATE INDEX IF NOT EXISTS accounting_periods_administration_period_idx
    ON accounting_periods (administration_id, period_start, period_end);

CREATE INDEX IF NOT EXISTS vat_periods_administration_period_idx
    ON vat_periods (administration_id, period_start, period_end);

CREATE INDEX IF NOT EXISTS vat_attention_items_period_unresolved_idx
    ON vat_attention_items (vat_period_id, resolved);

CREATE INDEX IF NOT EXISTS purchase_invoices_administration_date_status_idx
    ON purchase_invoices (administration_id, invoice_date, status);

COMMIT;
