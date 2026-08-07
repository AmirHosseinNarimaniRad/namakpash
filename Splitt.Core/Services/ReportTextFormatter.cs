using System.Text;
using Splitt.Core.Helpers;
using Splitt.Core.Models;

namespace Splitt.Core.Services;

/// <summary>
/// Renders the trip report as plain Persian text for sharing in a group chat.
/// Chat apps use proportional fonts, so no column alignment — one line per fact.
/// Lines lead with Persian text and end with the number so mixed-direction
/// (bidi) rendering stays stable; no trailing parentheses for the same reason.
/// </summary>
public static class ReportTextFormatter
{
    public static string Format(
        string tripName,
        IReadOnlyList<Participant> participants,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<ExpenseShare> shares)
    {
        var report = ReportBuilder.Build(participants, expenses, shares);
        var names = participants.ToDictionary(p => p.Id, p => p.Name);
        var sharesByExpense = shares.ToLookup(s => s.ExpenseId);
        var chronological = expenses.OrderBy(e => e.DateUtc).ThenBy(e => e.Id).ToList();

        var sb = new StringBuilder();
        sb.AppendLine(Bidi.Rtl($"🧾 گزارش «{tripName}»"));
        sb.AppendLine(Bidi.Rtl($"جمع هزینه‌ها: {MoneyFormat.FormatToman(report.Total)}"));
        sb.AppendLine(Bidi.Rtl($"میانگین هر نفر: {MoneyFormat.FormatToman(report.AveragePerPerson)}"));

        sb.AppendLine();
        sb.AppendLine(Bidi.Rtl("👥 خلاصهٔ افراد"));
        foreach (var p in report.People)
        {
            var status = p.Net == 0
                ? "تسویه"
                : $"{(p.Net > 0 ? "طلبکار" : "بدهکار")}: {MoneyFormat.Format(Math.Abs(p.Net))}";
            sb.AppendLine(Bidi.Rtl($"{p.Name} — پرداخت: {MoneyFormat.Format(p.Paid)} · سهم: {MoneyFormat.Format(p.Owed)} · {status}"));
        }

        var realExpenses = chronological.Where(e => !e.IsSettlement).ToList();
        if (realExpenses.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Bidi.Rtl("📋 ریز هزینه‌ها"));
            foreach (var e in realExpenses)
            {
                var description = e.Description.Length > 0 ? e.Description : "بدون شرح";
                var payer = names.GetValueOrDefault(e.PaidById, "؟");
                var date = PersianDate.ToDisplay(e.DateUtc.ToLocalTime());
                sb.AppendLine(Bidi.Rtl($"{description} · {date} · پرداخت: {payer} · {MoneyFormat.Format(e.Amount)}"));
            }
        }

        var settlements = chronological.Where(e => e.IsSettlement).ToList();
        if (settlements.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Bidi.Rtl("🤝 تسویه‌های ثبت‌شده"));
            foreach (var e in settlements)
            {
                var from = names.GetValueOrDefault(e.PaidById, "؟");
                var to = sharesByExpense[e.Id]
                    .Select(s => names.GetValueOrDefault(s.ParticipantId, "؟"))
                    .FirstOrDefault() ?? "؟";
                sb.AppendLine(Bidi.Rtl($"{from} به {to} — {MoneyFormat.FormatToman(e.Amount)}"));
            }
        }

        var net = BalanceCalculator.ComputeNet(participants, expenses, shares);
        var suggestions = SettlementPlanner.Plan(net);
        if (suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Bidi.Rtl("پیشنهاد تسویه"));
            foreach (var s in suggestions)
            {
                sb.AppendLine(Bidi.Rtl(
                    $"{names.GetValueOrDefault(s.FromParticipantId, "؟")} به {names.GetValueOrDefault(s.ToParticipantId, "؟")} — {MoneyFormat.FormatToman(s.Amount)}"));
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}
