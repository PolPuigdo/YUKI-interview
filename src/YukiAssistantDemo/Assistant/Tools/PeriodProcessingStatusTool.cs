using Npgsql;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Assistant.Tools;

public sealed class PeriodProcessingStatusTool(NpgsqlConnectionFactory connections) : IPeriodProcessingStatusTool
{
    public async Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT id, status, processed_through, updated_at
            FROM accounting_periods
            WHERE administration_id = @administration_id AND period_start = @period_start AND period_end = @period_end
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("administration_id", scope.AdministrationId);
        command.Parameters.AddWithValue("period_start", period.Start);
        command.Parameters.AddWithValue("period_end", period.End);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new(null, "I couldn't determine the processing status because the period data is missing.");
        var id = reader.GetString(0);
        var facts = new PeriodStatusFacts(period, reader.GetString(1), reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)));
        return new(new("period_processing_status", facts, $"Accounting period {id}", [id], reader.GetFieldValue<DateTimeOffset>(3)));
    }
}
