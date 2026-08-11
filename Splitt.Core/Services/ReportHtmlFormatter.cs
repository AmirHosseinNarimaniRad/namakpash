using System.Text;
using Splitt.Core.Helpers;
using Splitt.Core.Models;

namespace Splitt.Core.Services;

/// <summary>
/// Renders the trip report as printable HTML, one fixed-size A4 block per page.
///
/// Pagination lives here rather than in CSS because the PDF writer draws the laid-out
/// page onto a canvas and cannot honour page-break rules. Every block therefore declares
/// its height up front and blocks are packed into pages by arithmetic, which is also what
/// keeps a section heading from being stranded at the foot of a page.
/// Rows are single-line by design (long text is ellipsised) so a row's height is a constant.
/// </summary>
public static class ReportHtmlFormatter
{
    private const int PageHeight = 842;
    private const int Margin = 40;
    private const int FooterSpace = 46;
    private const int UsableHeight = PageHeight - Margin - FooterSpace;

    private const int HeaderHeight = 168;
    private const int TitleHeight = 40;
    private const int HeadRowHeight = 28;
    private const int RowHeight = 26;
    private const int SpacerHeight = 16;

    /// <summary>A person's name and their column headings, kept in one block so that a
    /// breakdown continuing onto the next page repeats both — headings alone would leave
    /// the reader unable to tell whose rows they are looking at.</summary>
    private const int PersonHeadHeight = HeadRowHeight + 34;

    // Column widths as percentages of the 515px content width.
    private static readonly int[] People = [34, 22, 22, 22];
    private static readonly int[] Expenses = [32, 22, 20, 26];
    private static readonly int[] Settlements = [26, 26, 22, 26];
    private static readonly int[] Suggestions = [38, 38, 24];
    private static readonly int[] PersonItems = [30, 44, 26];

    private sealed record Block(string Html, int Height, bool StartsSection = false, bool IsHead = false);

    public static (string Html, int PageCount) Format(
        string tripName,
        IReadOnlyList<Participant> participants,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<ExpenseShare> shares,
        DateTime generatedLocal,
        string fontBaseUrl = "")
    {
        var report = ReportBuilder.Build(participants, expenses, shares);
        var names = participants.ToDictionary(p => p.Id, p => p.Name);
        var sharesByExpense = shares.ToLookup(s => s.ExpenseId);
        var chronological = expenses.OrderBy(e => e.DateUtc).ThenBy(e => e.Id).ToList();
        var realExpenses = chronological.Where(e => !e.IsSettlement).ToList();
        var settlements = chronological.Where(e => e.IsSettlement).ToList();

        var blocks = new List<Block>
        {
            new(Header(tripName, report, realExpenses.Count, participants.Count, generatedLocal),
                HeaderHeight),
        };

        // --- people ---
        blocks.Add(new Block(SectionTitle("خلاصهٔ افراد"), TitleHeight, StartsSection: true));
        blocks.Add(new Block(
            TableHead(["نام", "پرداخت", "سهم", "وضعیت"], ["right", "left", "left", "left"], People),
            HeadRowHeight, IsHead: true));
        foreach (var p in report.People)
        {
            var status = p.Net == 0
                ? "<span class=\"pill settled\">تسویه</span>"
                : p.Net > 0
                    ? $"<span class=\"pill credit\">طلبکار {Money(p.Net)}</span>"
                    : $"<span class=\"pill debit\">بدهکار {Money(-p.Net)}</span>";
            blocks.Add(new Block(
                Row([Text(p.Name), Number(Money(p.Paid)), Number(Money(p.Owed)), status],
                    ["right", "left", "left", "left"], People),
                RowHeight));
        }

        // --- expenses ---
        if (realExpenses.Count > 0)
        {
            blocks.Add(new Block(Spacer(), SpacerHeight));
            blocks.Add(new Block(SectionTitle("ریز هزینه‌ها"), TitleHeight, StartsSection: true));
            blocks.Add(new Block(
                TableHead(["شرح", "تاریخ", "پرداخت‌کننده", "مبلغ"], ["right", "left", "right", "left"], Expenses),
                HeadRowHeight, IsHead: true));
            foreach (var e in realExpenses)
            {
                var description = e.Description.Length > 0 ? e.Description : "بدون شرح";
                blocks.Add(new Block(
                    Row([
                        Text(description),
                        Number(PersianDate.ToDisplayWithTime(e.DateUtc.ToLocalTime())),
                        Text(names.GetValueOrDefault(e.PaidById, "؟")),
                        Number(Money(e.Amount)),
                    ], ["right", "left", "right", "left"], Expenses),
                    RowHeight));
            }
        }

        // --- where each person's share total actually came from ---
        var withShares = report.People.Where(p => p.ShareItems.Count > 0).ToList();
        if (withShares.Count > 0)
        {
            blocks.Add(new Block(Spacer(), SpacerHeight));
            blocks.Add(new Block(SectionTitle("ریز سهم هر نفر"), TitleHeight, StartsSection: true));
            foreach (var p in withShares)
            {
                blocks.Add(new Block(
                    PersonHead(p.Name, Money(p.Owed)),
                    PersonHeadHeight,
                    StartsSection: true,
                    IsHead: true));
                foreach (var item in p.ShareItems)
                {
                    blocks.Add(new Block(
                        Row([
                            Number(PersianDate.ToDisplayWithTime(item.DateUtc.ToLocalTime())),
                            Text(item.Description.Length > 0 ? item.Description : "بدون شرح"),
                            Number(Money(item.Amount)),
                        ], ["right", "right", "left"], PersonItems),
                        RowHeight));
                }
            }
        }

        // --- settlements already recorded ---
        if (settlements.Count > 0)
        {
            blocks.Add(new Block(Spacer(), SpacerHeight));
            blocks.Add(new Block(SectionTitle("تسویه‌های ثبت‌شده"), TitleHeight, StartsSection: true));
            blocks.Add(new Block(
                TableHead(["از", "به", "تاریخ", "مبلغ"], ["right", "right", "left", "left"], Settlements),
                HeadRowHeight, IsHead: true));
            foreach (var e in settlements)
            {
                var to = sharesByExpense[e.Id]
                    .Select(s => names.GetValueOrDefault(s.ParticipantId, "؟"))
                    .FirstOrDefault() ?? "؟";
                blocks.Add(new Block(
                    Row([
                        Text(names.GetValueOrDefault(e.PaidById, "؟")),
                        Text(to),
                        Number(PersianDate.ToDisplay(e.DateUtc.ToLocalTime())),
                        Number(Money(e.Amount)),
                    ], ["right", "right", "left", "left"], Settlements),
                    RowHeight));
            }
        }

        // --- what is still owed ---
        var net = BalanceCalculator.ComputeNet(participants, expenses, shares);
        var suggestions = SettlementPlanner.Plan(net);
        if (suggestions.Count > 0)
        {
            blocks.Add(new Block(Spacer(), SpacerHeight));
            blocks.Add(new Block(SectionTitle("پیشنهاد تسویه"), TitleHeight, StartsSection: true));
            blocks.Add(new Block(
                TableHead(["از", "به", "مبلغ"], ["right", "right", "left"], Suggestions),
                HeadRowHeight, IsHead: true));
            foreach (var s in suggestions)
            {
                blocks.Add(new Block(
                    Row([
                        Text(names.GetValueOrDefault(s.FromParticipantId, "؟")),
                        Text(names.GetValueOrDefault(s.ToParticipantId, "؟")),
                        Number(Money(s.Amount)),
                    ], ["right", "right", "left"], Suggestions),
                    RowHeight));
            }
        }

        var pages = Paginate(blocks);
        return (Document(pages, fontBaseUrl), pages.Count);
    }

    /// <summary>
    /// Packs blocks into pages. A section heading is never left as the last thing on a
    /// page: it moves down with the header row and first data row that follow it. When a
    /// table spills onto the next page its column headings are repeated at the top.
    /// </summary>
    private static List<List<Block>> Paginate(List<Block> blocks)
    {
        var pages = new List<List<Block>>();
        var current = new List<Block>();
        var used = 0;
        Block? openHead = null;

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.IsHead)
                openHead = block;

            // A heading only earns its place if its table head and first row fit too.
            var needed = block.Height;
            if (block.StartsSection)
                needed += blocks.Skip(i + 1).Take(2).Sum(b => b.Height);

            if (current.Count > 0 && used + needed > UsableHeight)
            {
                pages.Add(current);
                current = [];
                used = 0;

                // Mid-table break: carry the column headings over so the rows stay readable.
                if (block.Height == RowHeight && openHead is not null)
                {
                    current.Add(openHead);
                    used += openHead.Height;
                }
            }

            // A spacer at the top of a fresh page would just be dead air.
            if (current.Count == 0 && block.Height == SpacerHeight && block.Html.Length == 0)
                continue;

            current.Add(block);
            used += block.Height;
        }

        if (current.Count > 0)
            pages.Add(current);
        return pages.Count > 0 ? pages : [[]];
    }

    private static string Document(List<List<Block>> pages, string fontBaseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html dir=\"rtl\" lang=\"fa\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=595\">");
        sb.Append("<style>").Append(Css(fontBaseUrl)).Append("</style></head><body>");

        for (var i = 0; i < pages.Count; i++)
        {
            sb.Append("<section class=\"page\"><div class=\"content\">");
            foreach (var block in pages[i])
                sb.Append(block.Html);
            sb.Append("</div><div class=\"footer\"><span>نمک‌پاش</span>");
            sb.Append($"<span>صفحهٔ {i + 1} از {pages.Count}</span></div></section>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Css(string fontBaseUrl) => $$"""
        @font-face { font-family: "Vazirmatn"; src: url("{{fontBaseUrl}}Vazirmatn-Regular.ttf"); font-weight: 400; }
        @font-face { font-family: "Vazirmatn"; src: url("{{fontBaseUrl}}Vazirmatn-Bold.ttf"); font-weight: 700; }
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: "Vazirmatn", sans-serif; color: #16302E; background: #FFFFFF; }
        .page { position: relative; width: 595px; height: 842px; overflow: hidden;
                padding: 40px 40px 0; background: #FFFFFF; }
        .content { width: 515px; }
        .footer { position: absolute; left: 40px; right: 40px; bottom: 18px; height: 28px;
                  display: flex; justify-content: space-between; align-items: center;
                  border-top: 1px solid #E3EDEC; color: #8AA5A2; font-size: 9px; padding-top: 8px; }
        .head { border-bottom: 3px solid #14B8A6; padding-bottom: 14px; margin-bottom: 18px; }
        .eyebrow { font-size: 10px; color: #0B7F73; letter-spacing: 1px; }
        .trip { font-size: 26px; font-weight: 700; margin-top: 2px; }
        .meta { font-size: 10px; color: #6C8B88; margin-top: 6px; }
        .cards { display: flex; gap: 10px; margin-top: 14px; }
        .card { flex: 1; border: 1px solid #E3EDEC; border-radius: 8px; padding: 10px 12px; }
        .card .label { font-size: 9px; color: #6C8B88; }
        .card .value { font-size: 17px; font-weight: 700; direction: ltr; text-align: right; margin-top: 3px; }
        .card.accent { background: #F0FBFA; border-color: #B9E9E3; }
        h2 { font-size: 13px; font-weight: 700; height: 40px; line-height: 46px; }
        table { width: 100%; border-collapse: collapse; table-layout: fixed; }
        tr.headrow th { height: 28px; font-size: 9px; font-weight: 400; color: #6C8B88;
                        border-bottom: 1px solid #CFE3E1; vertical-align: middle; }
        td { height: 26px; font-size: 11px; border-bottom: 1px solid #F0F5F4;
             vertical-align: middle; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        td, th { padding: 0 8px; }
        td:first-child, th:first-child { padding-right: 0; }
        td:last-child, th:last-child { padding-left: 0; }
        .num { direction: ltr; font-variant-numeric: tabular-nums; }
        .r { text-align: right; }
        .l { text-align: left; }
        .pill { font-size: 9px; padding: 2px 7px; border-radius: 999px; }
        .credit { background: #E7F7EE; color: #12734A; }
        .debit { background: #FDEAEA; color: #A32020; }
        .settled { background: #EEF3F2; color: #6C8B88; }
        .spacer { height: 16px; }
        .person { height: 34px; display: flex; justify-content: space-between; align-items: center;
                  border-right: 3px solid #14B8A6; padding: 0 8px 0 0; margin-top: 4px; }
        .person .who { font-size: 12px; font-weight: 700; }
        .person .sum { font-size: 10px; color: #6C8B88; }
        """;

    private static string Header(
        string tripName, TripReport report, int expenseCount, int peopleCount, DateTime generated)
        => $"""
        <div class="head">
          <div class="eyebrow">گزارش سفر</div>
          <div class="trip">{Escape(tripName)}</div>
          <div class="meta">{peopleCount} نفر · {expenseCount} هزینه · تاریخ گزارش: {PersianDate.ToLongDisplay(generated)}</div>
        </div>
        <div class="cards">
          <div class="card accent"><div class="label">جمع هزینه‌ها</div><div class="value">{Money(report.Total)}</div></div>
          <div class="card"><div class="label">میانگین هر نفر</div><div class="value">{Money(report.AveragePerPerson)}</div></div>
        </div>
        """;

    private static string SectionTitle(string text) => $"<h2>{text}</h2>";

    /// <summary>A person's name with their share total, above that person's column headings.</summary>
    private static string PersonHead(string name, string total) =>
        $"""
        <div class="person"><span class="who">{Escape(name)}</span>
        <span class="sum">مجموع سهم: <span class="num">{total}</span></span></div>
        """ + TableHead(["تاریخ", "شرح", "سهم"], ["right", "right", "left"], PersonItems);

    private static string Spacer() => "<div class=\"spacer\"></div>";

    private static string TableHead(string[] labels, string[] align, int[] widths)
    {
        var sb = new StringBuilder("<table><tr class=\"headrow\">");
        for (var i = 0; i < labels.Length; i++)
            sb.Append($"<th class=\"{Align(align[i])}\" width=\"{widths[i]}%\">{labels[i]}</th>");
        return sb.Append("</tr></table>").ToString();
    }

    private static string Row(string[] cells, string[] align, int[] widths)
    {
        var sb = new StringBuilder("<table><tr>");
        for (var i = 0; i < cells.Length; i++)
            sb.Append($"<td class=\"{Align(align[i])}\" width=\"{widths[i]}%\">{cells[i]}</td>");
        return sb.Append("</tr></table>").ToString();
    }

    private static string Align(string a) => a == "right" ? "r" : "l";

    private static string Text(string value) => Escape(value);

    private static string Number(string value) => $"<span class=\"num\">{value}</span>";

    private static string Money(decimal value) => MoneyFormat.Format(value);

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
