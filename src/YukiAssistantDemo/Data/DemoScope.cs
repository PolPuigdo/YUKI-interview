namespace YukiAssistantDemo.Data;

public sealed record DemoScope(string TenantId, string AdministrationId, string Market, string Currency)
{
    public static DemoScope FromConfiguration(IConfiguration configuration) => new(
        configuration["DEMO_TENANT_ID"] ?? configuration["DemoScope:TenantId"] ?? "demo-tenant",
        configuration["DEMO_ADMINISTRATION_ID"] ?? configuration["DemoScope:AdministrationId"] ?? "northstar-bikes-nl",
        configuration["DEMO_MARKET"] ?? configuration["DemoScope:Market"] ?? "NL",
        configuration["DEMO_CURRENCY"] ?? configuration["DemoScope:Currency"] ?? "EUR");
}
