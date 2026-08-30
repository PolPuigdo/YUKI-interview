BEGIN;

-- All values are synthetic and belong to the single server-owned demo scope.
INSERT INTO administrations (id, tenant_id, name, market, currency, updated_at)
VALUES ('northstar-bikes-nl', 'demo-tenant', 'Northstar Bikes B.V.', 'NL', 'EUR', CURRENT_TIMESTAMP)
ON CONFLICT (id) DO UPDATE SET
    tenant_id = EXCLUDED.tenant_id,
    name = EXCLUDED.name,
    market = EXCLUDED.market,
    currency = EXCLUDED.currency,
    updated_at = EXCLUDED.updated_at;

WITH dates AS (
    SELECT
        (date_trunc('month', CURRENT_DATE) - interval '1 month')::date AS period_start,
        (date_trunc('month', CURRENT_DATE) - interval '1 day')::date AS period_end
)
INSERT INTO accounting_periods (
    id, administration_id, period_start, period_end, status, processed_through, updated_at
)
SELECT
    'accounting-period-previous-month',
    'northstar-bikes-nl',
    period_start,
    period_end,
    'PROCESSED',
    period_end,
    CURRENT_TIMESTAMP
FROM dates
ON CONFLICT (id) DO UPDATE SET
    administration_id = EXCLUDED.administration_id,
    period_start = EXCLUDED.period_start,
    period_end = EXCLUDED.period_end,
    status = EXCLUDED.status,
    processed_through = EXCLUDED.processed_through,
    updated_at = EXCLUDED.updated_at;

WITH dates AS (
    SELECT
        date_trunc('quarter', CURRENT_DATE)::date AS period_start,
        (date_trunc('quarter', CURRENT_DATE) + interval '3 months - 1 day')::date AS period_end
)
INSERT INTO vat_periods (
    id, administration_id, period_start, period_end, deadline, status, updated_at
)
SELECT
    'vat-period-current',
    'northstar-bikes-nl',
    period_start,
    period_end,
    period_end + 30,
    'DRAFT',
    CURRENT_TIMESTAMP
FROM dates
ON CONFLICT (id) DO UPDATE SET
    administration_id = EXCLUDED.administration_id,
    period_start = EXCLUDED.period_start,
    period_end = EXCLUDED.period_end,
    deadline = EXCLUDED.deadline,
    status = EXCLUDED.status,
    updated_at = EXCLUDED.updated_at;

INSERT INTO vat_attention_items (
    id, vat_period_id, administration_id, item_type, label, resolved, source_ref, updated_at
)
VALUES
    ('vat-attention-purchase-01', 'vat-period-current', 'northstar-bikes-nl', 'MISSING_PURCHASE_INVOICE', 'Missing purchase invoice from supplier Delta Office', false, 'missing-purchase-delta-office', CURRENT_TIMESTAMP),
    ('vat-attention-purchase-02', 'vat-period-current', 'northstar-bikes-nl', 'MISSING_PURCHASE_INVOICE', 'Missing purchase invoice from supplier Harbor Logistics', false, 'missing-purchase-harbor-logistics', CURRENT_TIMESTAMP),
    ('vat-attention-sales-01', 'vat-period-current', 'northstar-bikes-nl', 'MISSING_SALES_INVOICE', 'Missing sales invoice for wholesale order NL-DEMO-0042', false, 'missing-sales-nl-demo-0042', CURRENT_TIMESTAMP),
    ('vat-attention-question-01', 'vat-period-current', 'northstar-bikes-nl', 'OPEN_QUESTION', 'Open question about the VAT treatment of a bicycle lease', false, 'open-question-lease-vat', CURRENT_TIMESTAMP)
ON CONFLICT (id) DO UPDATE SET
    vat_period_id = EXCLUDED.vat_period_id,
    administration_id = EXCLUDED.administration_id,
    item_type = EXCLUDED.item_type,
    label = EXCLUDED.label,
    resolved = EXCLUDED.resolved,
    source_ref = EXCLUDED.source_ref,
    updated_at = EXCLUDED.updated_at;

WITH dates AS (
    SELECT
        date_trunc('quarter', CURRENT_DATE)::date AS quarter_start,
        (date_trunc('quarter', CURRENT_DATE) + interval '3 months - 1 day')::date AS quarter_end,
        (date_trunc('quarter', CURRENT_DATE) - interval '1 day')::date AS previous_quarter_date
),
invoice_rows (id, supplier_name, invoice_number, day_offset, net_amount, vat_amount, status) AS (
    VALUES
        ('purchase-invoice-q-current-01', 'Canal Office Supplies', 'PO-DEMO-001', 1, 1250.00::numeric, 262.50::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-02', 'Delta Office', 'PO-DEMO-002', 2, 980.00::numeric, 205.80::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-03', 'Harbor Logistics', 'PO-DEMO-003', 3, 2400.00::numeric, 504.00::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-04', 'North Sea Energy', 'PO-DEMO-004', 4, 675.50::numeric, 141.86::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-05', 'Veluwe Components', 'PO-DEMO-005', 5, 3100.00::numeric, 651.00::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-06', 'Polder Insurance', 'PO-DEMO-006', 6, 1499.50::numeric, 314.90::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-07', 'Rijn Maintenance', 'PO-DEMO-007', 7, 1275.00::numeric, 267.75::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-08', 'Tulip Telecom', 'PO-DEMO-008', 8, 1280.00::numeric, 268.80::numeric, 'PROCESSED'),
        ('purchase-invoice-q-current-draft', 'Future Supplier', 'PO-DEMO-009', 9, 999.00::numeric, 209.79::numeric, 'DRAFT'),
        ('purchase-invoice-previous-quarter-01', 'Prior Quarter Supplier', 'PO-DEMO-000', 10, 5000.00::numeric, 1050.00::numeric, 'PROCESSED')
)
INSERT INTO purchase_invoices (
    id, administration_id, supplier_name, invoice_number, invoice_date, net_amount, vat_amount, status, updated_at
)
SELECT
    invoice_rows.id,
    'northstar-bikes-nl',
    invoice_rows.supplier_name,
    invoice_rows.invoice_number,
    CASE
        WHEN invoice_rows.id = 'purchase-invoice-previous-quarter-01' THEN dates.previous_quarter_date
        ELSE dates.quarter_start + invoice_rows.day_offset - 1
    END,
    invoice_rows.net_amount,
    invoice_rows.vat_amount,
    invoice_rows.status,
    CURRENT_TIMESTAMP
FROM invoice_rows
CROSS JOIN dates
ON CONFLICT (id) DO UPDATE SET
    administration_id = EXCLUDED.administration_id,
    supplier_name = EXCLUDED.supplier_name,
    invoice_number = EXCLUDED.invoice_number,
    invoice_date = EXCLUDED.invoice_date,
    net_amount = EXCLUDED.net_amount,
    vat_amount = EXCLUDED.vat_amount,
    status = EXCLUDED.status,
    updated_at = EXCLUDED.updated_at;

COMMIT;
