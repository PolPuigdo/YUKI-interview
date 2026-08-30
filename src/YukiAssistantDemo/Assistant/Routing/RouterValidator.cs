using System.Text.Json;
using System.Text.Json.Serialization;

namespace YukiAssistantDemo.Assistant.Routing;

public sealed class RouterValidator
{
    private static readonly HashSet<string> RequiredFields = ["intent", "period", "confidence", "clarification"];

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RouterValidator()
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }

    public bool TryValidate(string json, out RouterDecision? decision, out string? error)
    {
        decision = null;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Router output must be a JSON object.";
                return false;
            }

            var fields = document.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToHashSet();
            if (!RequiredFields.IsSubsetOf(fields))
            {
                error = "Router output is missing a required field.";
                return false;
            }

            decision = JsonSerializer.Deserialize<RouterDecision>(json, _jsonOptions);
            if (decision is null || decision.ExtraFields is { Count: > 0 })
            {
                error = "Router output contains unknown fields.";
                return false;
            }

            if (decision.Confidence is < 0 or > 1 || double.IsNaN(decision.Confidence) || double.IsInfinity(decision.Confidence))
            {
                error = "Confidence must be a finite number between 0 and 1.";
                return false;
            }

            if (!IsValidCombination(decision))
            {
                error = "Intent and period combination is invalid.";
                return false;
            }

            if (decision.Intent == RouterIntent.Clarify && string.IsNullOrWhiteSpace(decision.Clarification))
            {
                error = "Clarification intent requires a clarification question.";
                return false;
            }

            if (decision.Intent != RouterIntent.Clarify && decision.Clarification is not null)
            {
                error = "Clarification must be null for non-clarification routes.";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Router output is not valid JSON for the routing contract.";
            return false;
        }
    }

    private static bool IsValidCombination(RouterDecision decision) => decision.Intent switch
    {
        RouterIntent.PeriodProcessingStatus => decision.Period == RouterPeriod.LastMonth,
        RouterIntent.VatMissingItems => decision.Period == RouterPeriod.CurrentVatPeriod,
        RouterIntent.SupplierSpend => decision.Period == RouterPeriod.CurrentQuarter,
        RouterIntent.Clarify => decision.Period is null,
        RouterIntent.Unsupported => decision.Period is null,
        _ => false
    };
}
