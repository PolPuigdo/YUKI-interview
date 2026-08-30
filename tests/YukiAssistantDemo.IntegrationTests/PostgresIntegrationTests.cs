using Microsoft.Extensions.Configuration;
using YukiAssistantDemo.Assistant.Tools;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.IntegrationTests;

public sealed class PostgresIntegrationTests
{
    private static readonly DemoScope Scope = new("demo-tenant", "northstar-bikes-nl", "NL", "EUR");

    private static NpgsqlConnectionFactory Connections()
    {
        var connectionString = Environment.GetEnvironmentVariable("YUKI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("YUKI_TEST_CONNECTION is required for PostgreSQL integration tests.");

        return new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:YukiDemo"] = connectionString
        }).Build());
    }

    [Fact]
    public async Task Seeded_status_is_previous_month_processed()
    {
        var period = new DatePeriodResolver(new FixedClock(DateOnly.FromDateTime(DateTime.UtcNow))).LastMonth();
        var result = await new PeriodProcessingStatusTool(Connections()).ExecuteAsync(Scope, period, default);

        var facts = Assert.IsType<PeriodStatusFacts>(result.Evidence!.Facts);
        Assert.Equal("PROCESSED", facts.Status);
        Assert.Equal(period.End, facts.ProcessedThrough);
        Assert.Equal(["accounting-period-previous-month"], result.Evidence.SourceIds);
    }

    [Fact]
    public async Task Seeded_vat_returns_exact_unresolved_items()
    {
        var period = new DatePeriodResolver(new FixedClock(DateOnly.FromDateTime(DateTime.UtcNow))).CurrentQuarter();
        var result = await new VatAttentionTool(Connections()).ExecuteAsync(Scope, period, default);

        var facts = Assert.IsType<VatAttentionFacts>(result.Evidence!.Facts);
        Assert.Equal("DRAFT", facts.Status);
        Assert.Equal(4, facts.Items.Count);
        Assert.Equal(2, facts.Items.Count(x => x.Type == "MISSING_PURCHASE_INVOICE"));
        Assert.Equal(1, facts.Items.Count(x => x.Type == "MISSING_SALES_INVOICE"));
        Assert.Equal(1, facts.Items.Count(x => x.Type == "OPEN_QUESTION"));
        Assert.Contains("vat-period-current", result.Evidence.SourceIds);
    }

    [Fact]
    public async Task Seeded_supplier_spend_excludes_draft_and_previous_quarter()
    {
        var period = new DatePeriodResolver(new FixedClock(DateOnly.FromDateTime(DateTime.UtcNow))).CurrentQuarter();
        var result = await new SupplierSpendTool(Connections()).ExecuteAsync(Scope, period, default);

        var facts = Assert.IsType<SupplierSpendFacts>(result.Evidence!.Facts);
        Assert.Equal("EUR", facts.Currency);
        Assert.Equal(12460.00m, facts.NetAmount);
        Assert.Equal(8, facts.InvoiceCount);
        Assert.DoesNotContain("purchase-invoice-q-current-draft", result.Evidence.SourceIds);
        Assert.DoesNotContain("purchase-invoice-previous-quarter-01", result.Evidence.SourceIds);
        Assert.Equal(8, result.Evidence.SourceIds.Count);
    }

    private sealed class FixedClock(DateOnly today) : ISystemClock
    {
        public DateOnly Today => today;
    }
}
