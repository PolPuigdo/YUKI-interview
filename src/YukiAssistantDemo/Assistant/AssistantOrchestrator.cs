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
        var correlationId = Activity.Current?.TraceId.ToString() ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var llm = Stopwatch.StartNew();
        RouterResult routed;
        try
        {
            routed = await router.RouteAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRequestError(ex, correlationId, total, llm.ElapsedMilliseconds, null, null, null, "llm_router_exception", "failure", null);
            return new("failure", "I couldn't reach the local LLM right now. Please try again later.", null, null);
        }
        var llmMs = llm.ElapsedMilliseconds;
        if (!routed.IsExecutable)
        {
            var response = routed.Outcome switch
            {
                RouterOutcome.Clarification or RouterOutcome.LowConfidence => new ChatResponse("clarification", routed.Decision?.Clarification ?? routed.Message ?? "Please clarify which supported question you mean.", ToIntent(routed.Decision?.Intent), null),
                RouterOutcome.Unsupported => new ChatResponse("unsupported", "I can answer only about last month's processing status, missing VAT items, or supplier spend this quarter.", "unsupported", null),
                _ => new ChatResponse("failure", routed.Message ?? "I couldn't route that request reliably. Please try one of the three supported questions.", null, null)
            };
            LogRequest(correlationId, total, llmMs, ToIntent(routed.Decision?.Intent), routed.Decision?.Confidence, null, null, response.Outcome, SafeExitReason(routed), 0);
            return response;
        }

        ToolResult result;
        var toolName = ToolName(routed.Decision!.Intent);
        var tool = Stopwatch.StartNew();
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
            LogRequestError(ex, correlationId, total, llmMs, toolName, tool.ElapsedMilliseconds, routed.Decision.Confidence, "database_unavailable", "failure", ToIntent(routed.Decision.Intent));
            return new("failure", "I couldn't reach the accounting data right now. Please try again later.", ToIntent(routed.Decision!.Intent), null);
        }
        var toolMs = tool.ElapsedMilliseconds;

        if (!result.HasData)
        {
            LogRequest(correlationId, total, llmMs, ToIntent(routed.Decision.Intent), routed.Decision.Confidence, toolName, toolMs, "failure", "source_data_missing", 0);
            return new("failure", result.NoDataMessage ?? "The required source data is missing.", ToIntent(routed.Decision.Intent), null);
        }

        try
        {
            var evidence = result.Evidence!;
            var answer = renderer.Render(evidence);
            LogRequest(correlationId, total, llmMs, evidence.Intent, routed.Decision.Confidence, toolName, toolMs, "supported", null, evidence.SourceIds.Count);
            return new("supported", answer, evidence.Intent, new(evidence.Summary, evidence.Facts, evidence.SourceIds, evidence.Freshness));
        }
        catch (InvalidOperationException ex)
        {
            LogRequestError(ex, correlationId, total, llmMs, toolName, toolMs, routed.Decision.Confidence, "evidence_validation_failed", "failure", ToIntent(routed.Decision.Intent));
            return new("failure", "I couldn't validate the source data safely.", ToIntent(routed.Decision.Intent), null);
        }
    }

    private void LogRequest(string correlationId, Stopwatch total, long llmMs, string? intent, double? confidence,
        string? toolName, long? toolMs, string outcome, string? safeExitReason, int sourceCount) =>
        logger.LogInformation(
            "Assistant request completed {correlation_id} {outcome} {intent} {confidence} {llm_ms} {tool_name} {tool_ms} {source_count} {total_ms} {safe_exit_reason}",
            correlationId, outcome, intent, confidence, llmMs, toolName, toolMs, sourceCount, total.ElapsedMilliseconds, safeExitReason ?? "none");

    private void LogRequestError(Exception exception, string correlationId, Stopwatch total, long llmMs, string? toolName,
        long? toolMs, double? confidence, string safeExitReason, string outcome, string? intent) =>
        logger.LogError(
            exception,
            "Assistant request failed {correlation_id} {outcome} {intent} {confidence} {llm_ms} {tool_name} {tool_ms} {source_count} {total_ms} {safe_exit_reason}",
            correlationId, outcome, intent, confidence, llmMs, toolName, toolMs, 0, total.ElapsedMilliseconds, safeExitReason);

    private static string? ToolName(RouterIntent intent) => intent switch
    {
        RouterIntent.PeriodProcessingStatus => "period_processing_status",
        RouterIntent.VatMissingItems => "vat_missing_items",
        RouterIntent.SupplierSpend => "supplier_spend",
        _ => null
    };

    private static string SafeExitReason(RouterResult result) => result.Outcome switch
    {
        RouterOutcome.Unsupported => "unsupported",
        RouterOutcome.Clarification => "clarification",
        RouterOutcome.LowConfidence => "low_confidence",
        RouterOutcome.Failure when result.Message?.Contains("unavailable", StringComparison.OrdinalIgnoreCase) == true => "llm_unavailable",
        RouterOutcome.Failure when result.Message?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true => "llm_timeout",
        RouterOutcome.Failure => "router_failure",
        _ => "router_failure"
    };

    private static string? ToIntent(RouterIntent? intent) => intent switch
    {
        RouterIntent.PeriodProcessingStatus => "period_processing_status",
        RouterIntent.VatMissingItems => "vat_missing_items",
        RouterIntent.SupplierSpend => "supplier_spend",
        RouterIntent.Unsupported => "unsupported",
        _ => null
    };
}
