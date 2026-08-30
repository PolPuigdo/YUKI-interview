using System.Text.Json;
using System.Text.Json.Serialization;

namespace YukiAssistantDemo.Assistant.Routing;

public enum RouterIntent
{
    PeriodProcessingStatus,
    VatMissingItems,
    SupplierSpend,
    Clarify,
    Unsupported
}

public enum RouterPeriod
{
    LastMonth,
    CurrentVatPeriod,
    CurrentQuarter
}

public enum RouterOutcome
{
    Supported,
    Clarification,
    Unsupported,
    LowConfidence,
    Failure
}

public sealed class RouterDecision
{
    [JsonPropertyName("intent")]
    public RouterIntent Intent { get; init; }

    [JsonPropertyName("period")]
    public RouterPeriod? Period { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("clarification")]
    public string? Clarification { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record RouterResult(
    RouterOutcome Outcome,
    RouterDecision? Decision = null,
    string? Message = null)
{
    public bool IsExecutable => Outcome == RouterOutcome.Supported && Decision is not null;
}

public interface ILlmRouter
{
    Task<RouterResult> RouteAsync(string message, CancellationToken cancellationToken = default);
}
