using Npgsql;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Assistant.Tools;

public sealed class VatAttentionTool(NpgsqlConnectionFactory connections) : IVatAttentionTool
{
    public async Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var periodCommand = new NpgsqlCommand("""
            SELECT id, status, deadline, updated_at FROM vat_periods
            WHERE administration_id = @administration_id AND period_start = @period_start AND period_end = @period_end
            ORDER BY period_start DESC LIMIT 1
            """, connection);
        periodCommand.Parameters.AddWithValue("administration_id", scope.AdministrationId);
        periodCommand.Parameters.AddWithValue("period_start", period.Start);
        periodCommand.Parameters.AddWithValue("period_end", period.End);
        await using var periodReader = await periodCommand.ExecuteReaderAsync(cancellationToken);
        if (!await periodReader.ReadAsync(cancellationToken)) return new(null, "I couldn't determine the current VAT period because the period data is missing.");
        var periodId = periodReader.GetString(0);
        var status = periodReader.GetString(1);
        var deadline = DateOnly.FromDateTime(periodReader.GetDateTime(2));
        var freshness = periodReader.GetFieldValue<DateTimeOffset>(3);
        await periodReader.DisposeAsync();

        await using var itemsCommand = new NpgsqlCommand("""
            SELECT id, item_type, label, updated_at FROM vat_attention_items
            WHERE administration_id = @administration_id AND vat_period_id = @vat_period_id AND resolved = false ORDER BY id
            """, connection);
        itemsCommand.Parameters.AddWithValue("administration_id", scope.AdministrationId);
        itemsCommand.Parameters.AddWithValue("vat_period_id", periodId);
        await using var itemsReader = await itemsCommand.ExecuteReaderAsync(cancellationToken);
        var items = new List<VatAttentionItem>();
        var sourceIds = new List<string> { periodId };
        while (await itemsReader.ReadAsync(cancellationToken))
        {
            var id = itemsReader.GetString(0);
            items.Add(new(id, itemsReader.GetString(1), itemsReader.GetString(2)));
            sourceIds.Add(id);
            var itemFreshness = itemsReader.GetFieldValue<DateTimeOffset>(3);
            if (itemFreshness > freshness) freshness = itemFreshness;
        }
        var facts = new VatAttentionFacts(period, status, deadline, items);
        return new(new("vat_missing_items", facts, $"VAT period {periodId} and unresolved attention items", sourceIds, freshness));
    }
}
