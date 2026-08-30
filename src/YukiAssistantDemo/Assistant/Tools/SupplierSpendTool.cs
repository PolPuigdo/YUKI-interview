using Npgsql;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Assistant.Tools;

public sealed class SupplierSpendTool(NpgsqlConnectionFactory connections) : ISupplierSpendTool
{
    public async Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT COALESCE(SUM(net_amount), 0), COUNT(*), COALESCE(MAX(updated_at), CURRENT_TIMESTAMP)
            FROM purchase_invoices WHERE administration_id = @administration_id
              AND invoice_date >= @period_start AND invoice_date <= @period_end AND status = 'PROCESSED'
            """, connection);
        command.Parameters.AddWithValue("administration_id", scope.AdministrationId);
        command.Parameters.AddWithValue("period_start", period.Start);
        command.Parameters.AddWithValue("period_end", period.End);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var amount = reader.GetDecimal(0);
        var count = reader.GetInt64(1);
        var freshness = reader.GetFieldValue<DateTimeOffset>(2);
        await reader.DisposeAsync();
        if (count == 0) return new(null, "I couldn't determine supplier spend because no processed invoices were found for this quarter.");

        await using var idsCommand = new NpgsqlCommand("""
            SELECT id FROM purchase_invoices WHERE administration_id = @administration_id
              AND invoice_date >= @period_start AND invoice_date <= @period_end AND status = 'PROCESSED' ORDER BY id
            """, connection);
        idsCommand.Parameters.AddWithValue("administration_id", scope.AdministrationId);
        idsCommand.Parameters.AddWithValue("period_start", period.Start);
        idsCommand.Parameters.AddWithValue("period_end", period.End);
        await using var idsReader = await idsCommand.ExecuteReaderAsync(cancellationToken);
        var sourceIds = new List<string>();
        while (await idsReader.ReadAsync(cancellationToken)) sourceIds.Add(idsReader.GetString(0));
        var facts = new SupplierSpendFacts(period, scope.Currency, amount, checked((int)count));
        return new(new("supplier_spend", facts, "Processed purchase invoices in the current quarter", sourceIds, freshness));
    }
}
