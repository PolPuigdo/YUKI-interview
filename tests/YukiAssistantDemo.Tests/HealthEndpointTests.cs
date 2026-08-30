using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YukiAssistantDemo.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_successful_json_response()
    {
        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
    }

    [Fact]
    public async Task Static_chat_ui_and_assets_are_served()
    {
        using var page = await _client.GetAsync("/");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("/styles.css", html);
        Assert.Contains("/app.js", html);
        Assert.Contains("data-question", html);

        using var styles = await _client.GetAsync("/styles.css");
        using var script = await _client.GetAsync("/app.js");

        Assert.Equal(HttpStatusCode.OK, styles.StatusCode);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
    }

    private sealed record HealthResponse(string Status);
}
