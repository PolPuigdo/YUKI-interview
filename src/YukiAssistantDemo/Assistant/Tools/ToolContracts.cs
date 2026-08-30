using YukiAssistantDemo.Data;

namespace YukiAssistantDemo.Assistant.Tools;

public sealed record EvidenceBundle(string Intent, object Facts, string Summary, IReadOnlyList<string> SourceIds, DateTimeOffset Freshness);
public sealed record ToolResult(EvidenceBundle? Evidence, string? NoDataMessage = null) { public bool HasData => Evidence is not null; }
public sealed record PeriodStatusFacts(PeriodWindow Period, string Status, DateOnly? ProcessedThrough);
public sealed record VatAttentionItem(string Id, string Type, string Label);
public sealed record VatAttentionFacts(PeriodWindow Period, string Status, DateOnly Deadline, IReadOnlyList<VatAttentionItem> Items);
public sealed record SupplierSpendFacts(PeriodWindow Period, string Currency, decimal NetAmount, int InvoiceCount);

public interface IPeriodProcessingStatusTool { Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken); }
public interface IVatAttentionTool { Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken); }
public interface ISupplierSpendTool { Task<ToolResult> ExecuteAsync(DemoScope scope, PeriodWindow period, CancellationToken cancellationToken); }
