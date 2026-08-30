using System.Globalization;

namespace YukiAssistantDemo.Assistant.Routing;

public sealed class RouterOptions
{
    public string Provider { get; set; } = "ollama";
    public string BaseUrl { get; set; } = "http://host.docker.internal:11434/v1";
    public string Model { get; set; } = "qwen3.5:4b";
    public string ApiKey { get; set; } = "local-not-used";
    public int TimeoutSeconds { get; set; } = 60;
    public double ConfidenceThreshold { get; set; } = 0.70;

    public static RouterOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new RouterOptions
        {
            Provider = configuration["LLM_PROVIDER"] ?? configuration["LLM:Provider"] ?? "ollama",
            BaseUrl = configuration["LLM_BASE_URL"] ?? configuration["LLM:BaseUrl"] ?? "http://host.docker.internal:11434/v1",
            Model = configuration["LLM_MODEL"] ?? configuration["LLM:Model"] ?? "qwen3.5:4b",
            ApiKey = configuration["LLM_API_KEY"] ?? configuration["LLM:ApiKey"] ?? "local-not-used"
        };

        if (int.TryParse(configuration["LLM_TIMEOUT_SECONDS"] ?? configuration["LLM:TimeoutSeconds"], out var timeout))
            options.TimeoutSeconds = timeout;
        if (double.TryParse(configuration["ROUTER_CONFIDENCE_THRESHOLD"] ?? configuration["LLM:ConfidenceThreshold"], CultureInfo.InvariantCulture, out var threshold))
            options.ConfidenceThreshold = threshold;

        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (!string.Equals(Provider, "ollama", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Provider, "mlx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LLM_PROVIDER must be 'ollama' or 'mlx'.");
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("LLM_BASE_URL must be an absolute HTTP(S) URL.");
        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("LLM_MODEL is required.");
        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("LLM_TIMEOUT_SECONDS must be greater than zero.");
        if (ConfidenceThreshold is < 0 or > 1)
            throw new InvalidOperationException("ROUTER_CONFIDENCE_THRESHOLD must be between 0 and 1.");
    }
}
