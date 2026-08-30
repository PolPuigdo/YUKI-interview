using YukiAssistantDemo.Assistant.Routing;
using YukiAssistantDemo.Assistant;
using YukiAssistantDemo.Assistant.Rendering;
using YukiAssistantDemo.Assistant.Tools;
using YukiAssistantDemo.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(RouterOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<RouterValidator>();
builder.Services.AddHttpClient<OpenAiCompatibleLlmRouter>();
builder.Services.AddSingleton<ILlmRouter>(serviceProvider =>
    serviceProvider.GetRequiredService<OpenAiCompatibleLlmRouter>());
builder.Services.AddSingleton<DemoScope>(DemoScope.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<DatePeriodResolver>();
builder.Services.AddSingleton<NpgsqlConnectionFactory>();
builder.Services.AddSingleton<IPeriodProcessingStatusTool, PeriodProcessingStatusTool>();
builder.Services.AddSingleton<IVatAttentionTool, VatAttentionTool>();
builder.Services.AddSingleton<ISupplierSpendTool, SupplierSpendTool>();
builder.Services.AddSingleton<GroundedAnswerRenderer>();
builder.Services.AddSingleton<AssistantOrchestrator>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/api/chat", async (ChatRequest request, AssistantOrchestrator assistant, CancellationToken cancellationToken) =>
{
    if (request is null)
        return Results.BadRequest(new { error = "A message is required." });
    var response = await assistant.HandleAsync(request.Message ?? string.Empty, cancellationToken);
    return Results.Json(response, statusCode: response.Outcome == "failure" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
});

app.Run();

public partial class Program { }

public sealed record ChatRequest(string? Message);
