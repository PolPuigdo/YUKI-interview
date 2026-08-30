using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YukiAssistantDemo.Assistant.Rendering;
using YukiAssistantDemo.Assistant.Routing;
using YukiAssistantDemo.Assistant.Tools;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Tests;

public sealed class DatePeriodResolverTests
{
    [Fact]
    public void Resolves_january_to_previous_december()
    {
        var resolver = new DatePeriodResolver(new FixedClock(new(2026, 1, 15)));

        var period = resolver.LastMonth();

        Assert.Equal(new(2025, 12, 1), period.Start);
        Assert.Equal(new(2025, 12, 31), period.End);
    }

    private sealed class FixedClock(DateOnly today) : ISystemClock
    {
        public DateOnly Today => today;
    }
}

public sealed class GroundedAnswerRendererTests
{
    private readonly GroundedAnswerRenderer _renderer = new();

    [Fact]
    public void Renders_exact_supplier_total_from_evidence()
    {
        var evidence = new EvidenceBundle("supplier_spend", new SupplierSpendFacts(
            new(new(2026, 7, 1), new(2026, 9, 30)), "EUR", 12460.00m, 8), "summary", ["invoice-1"], DateTimeOffset.UtcNow);

        var answer = _renderer.Render(evidence);

        Assert.Contains("EUR 12,460.00", answer);
        Assert.Contains("8 processed purchase invoices", answer);
    }

    [Fact]
    public void Renders_vat_counts_from_unresolved_items()
    {
        var items = new[]
        {
            new VatAttentionItem("1", "MISSING_PURCHASE_INVOICE", "Purchase"),
            new VatAttentionItem("2", "MISSING_PURCHASE_INVOICE", "Purchase"),
            new VatAttentionItem("3", "MISSING_SALES_INVOICE", "Sales"),
            new VatAttentionItem("4", "OPEN_QUESTION", "Question")
        };
        var evidence = new EvidenceBundle("vat_missing_items", new VatAttentionFacts(
            new(new(2026, 7, 1), new(2026, 9, 30)), "DRAFT", new(2026, 10, 30), items), "summary", ["vat-period", "1"], DateTimeOffset.UtcNow);

        var answer = _renderer.Render(evidence);

        Assert.Contains("4 attention items", answer);
        Assert.Contains("2 purchase invoices", answer);
        Assert.Contains("1 sales invoices", answer);
        Assert.Contains("1 open question(s)", answer);
    }

    [Fact]
    public void Rejects_evidence_without_sources()
    {
        var evidence = new EvidenceBundle("supplier_spend", new SupplierSpendFacts(
            new(new(2026, 7, 1), new(2026, 9, 30)), "EUR", 1m, 1), "summary", [], DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => _renderer.Render(evidence));
    }
}

public sealed class AssistantApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AssistantApiTests(WebApplicationFactory<Program> factory)
    {
        var testFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILlmRouter>();
            services.RemoveAll<IPeriodProcessingStatusTool>();
            services.RemoveAll<IVatAttentionTool>();
            services.RemoveAll<ISupplierSpendTool>();
            services.AddSingleton<ILlmRouter>(new FakeRouter());
            services.AddSingleton<IPeriodProcessingStatusTool>(new FakeStatusTool());
            services.AddSingleton<IVatAttentionTool>(new FakeVatTool());
            services.AddSingleton<ISupplierSpendTool>(new FakeSpendTool());
        }));
        _client = testFactory.CreateClient();
    }

    [Fact]
    public async Task Unsupported_route_does_not_execute_a_tool()
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new { message = "What is the weather?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatBody>();
        Assert.Equal("unsupported", body!.Outcome);
        Assert.Null(body.Evidence);
    }

    [Fact]
    public async Task Supported_route_returns_answer_and_evidence_separately()
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new { message = "supplier spend" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatBody>();
        Assert.Equal("supported", body!.Outcome);
        Assert.Contains("EUR 12,460.00", body.Answer);
        Assert.NotNull(body.Evidence);
    }

    private sealed record ChatBody(string Outcome, string Answer, string? Intent, object? Evidence);

    private sealed class FakeRouter : ILlmRouter
    {
        public Task<RouterResult> RouteAsync(string message, CancellationToken cancellationToken = default)
        {
            if (message.Contains("weather", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new RouterResult(RouterOutcome.Unsupported));
            return Task.FromResult(new RouterResult(RouterOutcome.Supported, new RouterDecision
            {
                Intent = RouterIntent.SupplierSpend,
                Period = RouterPeriod.CurrentQuarter,
                Confidence = 0.99
            }));
        }
    }

    private sealed class FakeStatusTool : IPeriodProcessingStatusTool
    {
        public Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken) => Task.FromResult<ToolResult>(new(null, "unused"));
    }
    private sealed class FakeVatTool : IVatAttentionTool
    {
        public Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken) => Task.FromResult<ToolResult>(new(null, "unused"));
    }
    private sealed class FakeSpendTool : ISupplierSpendTool
    {
        public Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken) => Task.FromResult(new ToolResult(new EvidenceBundle(
            "supplier_spend", new SupplierSpendFacts(period, "EUR", 12460.00m, 8), "Processed invoices", ["invoice-1"], DateTimeOffset.UtcNow)));
    }
}
