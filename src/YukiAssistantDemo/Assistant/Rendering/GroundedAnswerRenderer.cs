using System.Globalization;
using YukiAssistantDemo.Assistant.Tools;

namespace YukiAssistantDemo.Assistant.Rendering;

public sealed class GroundedAnswerRenderer
{
    public string Render(EvidenceBundle evidence)
    {
        if (evidence.SourceIds.Count == 0 || string.IsNullOrWhiteSpace(evidence.Summary))
            throw new InvalidOperationException("Grounded evidence must contain a summary and source IDs.");
        return evidence.Facts switch
        {
            PeriodStatusFacts status => RenderStatus(status),
            VatAttentionFacts vat => RenderVat(vat),
            SupplierSpendFacts spend => RenderSpend(spend),
            _ => throw new InvalidOperationException("Unsupported evidence facts.")
        };
    }

    private static string RenderStatus(PeriodStatusFacts facts)
    {
        if (facts.Status == "PROCESSED" && facts.ProcessedThrough is null)
            throw new InvalidOperationException("Processed status is missing processed-through evidence.");
        return facts.Status == "PROCESSED"
            ? $"Yes. {facts.Period.Start:MMMM yyyy} is marked as Processed. Processed through {facts.ProcessedThrough:dd MMMM yyyy}."
            : $"{facts.Period.Start:MMMM yyyy} is marked as {facts.Status}.";
    }

    private static string RenderVat(VatAttentionFacts facts)
    {
        var purchase = facts.Items.Count(x => x.Type == "MISSING_PURCHASE_INVOICE");
        var sales = facts.Items.Count(x => x.Type == "MISSING_SALES_INVOICE");
        var questions = facts.Items.Count(x => x.Type == "OPEN_QUESTION");
        return $"Your current VAT period is {facts.Status}. You're still missing {facts.Items.Count} attention items: {purchase} purchase invoices, {sales} sales invoices, and {questions} open question(s). Demo deadline: {facts.Deadline:dd MMMM yyyy}.";
    }

    private static string RenderSpend(SupplierSpendFacts facts) =>
        string.IsNullOrWhiteSpace(facts.Currency)
            ? throw new InvalidOperationException("Supplier spend evidence is missing currency.")
            : $"You spent {facts.Currency} {facts.NetAmount.ToString("N2", CultureInfo.InvariantCulture)} excluding VAT on suppliers this quarter across {facts.InvoiceCount} processed purchase invoices.";
}
