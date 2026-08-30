using System.Diagnostics;
using System.Data.Common;
using YukiAssistantDemo.Assistant.Rendering;
using YukiAssistantDemo.Assistant.Routing;
using YukiAssistantDemo.Assistant.Tools;
using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Assistant;

public sealed record ChatResponse(string Outcome, string Answer, string? Intent, ChatEvidence? Evidence);
public sealed record ChatEvidence(string Summary, object Facts, IReadOnlyList<string> SourceIds, DateTimeOffset Freshness);

public sealed class AssistantOrchestrator(
    ILlmRouter router, DemoScope scope, DatePeriodResolver periods,
    IPeriodProcessingStatusTool statusTool, IVatAttentionTool vatTool, ISupplierSpendTool spendTool,
    GroundedAnswerRenderer renderer, ILogger<AssistantOrchestrator> logger)
{
    public async Task<ChatResponse> HandleAsync(string message, CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        var routed = await router.RouteAsync(message, cancellationToken);
        if (!routed.IsExecutable)
        {
            var response = routed.Outcome switch
            {
                RouterOutcome.Clarification or RouterOutcome.LowConfidence => new ChatResponse("clarification", routed.Decision?.Clarification ?? routed.Message ?? "Please clarify which supported question you mean.", ToIntent(routed.Decision?.Intent), null),
                RouterOutcome.Unsupported => new ChatResponse("unsupported", "I can answer only about last month's processing status, missing VAT items, or supplier spend this quarter.", "unsupported", null),
                _ => new ChatResponse("failure", routed.Message ?? "I couldn't route that request reliably. Please try one of the three supported questions.", null, null)
            };
            logger.LogInformation("Assistant safe exit: {Outcome} in {TotalMs} ms", response.Outcome, total.ElapsedMilliseconds);
            return response;
        }

        ToolResult result;
        try
        {
            result = routed.Decision!.Intent switch
            {
                RouterIntent.PeriodProcessingStatus => await statusTool.ExecuteAsync(scope, periods.LastMonth(), cancellationToken),
                RouterIntent.VatMissingItems => await vatTool.ExecuteAsync(scope, periods.CurrentQuarter(), cancellationToken),
                RouterIntent.SupplierSpend => await spendTool.ExecuteAsync(scope, periods.CurrentQuarter(), cancellationToken),
                _ => throw new InvalidOperationException("Validated route is not executable.")
            };
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Assistant database access failed");
            return new("failure", "I couldn't reach the accounting data right now. Please try again later.", ToIntent(routed.Decision!.Intent), null);
        }

        if (!result.HasData)
            return new("failure", result.NoDataMessage ?? "The required source data is missing.", ToIntent(routed.Decision.Intent), null);

        try
        {
            var evidence = result.Evidence!;
            var answer = renderer.Render(evidence);
            logger.LogInformation("Assistant grounded response {Intent} with {SourceCount} sources in {TotalMs} ms", evidence.Intent, evidence.SourceIds.Count, total.ElapsedMilliseconds);
            return new("supported", answer, evidence.Intent, new(evidence.Summary, evidence.Facts, evidence.SourceIds, evidence.Freshness));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Grounded response validation failed");
            return new("failure", "I couldn't validate the source data safely.", ToIntent(routed.Decision.Intent), null);
        }
    }

    private static string? ToIntent(RouterIntent? intent) => intent switch
    {
        RouterIntent.PeriodProcessingStatus => "period_processing_status",
        RouterIntent.VatMissingItems => "vat_missing_items",
        RouterIntent.SupplierSpend => "supplier_spend",
        RouterIntent.Unsupported => "unsupported",
        _ => null
    };
}
