using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.Tests;

public class ReportHtmlFormatterTests
{
    private static readonly DateTime Generated = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Local);

    private static (List<Participant>, List<Expense>, List<ExpenseShare>) Trip(int expenseCount)
    {
        List<Participant> people =
        [
            new() { Id = 1, TripId = 1, Name = "Sara" },
            new() { Id = 2, TripId = 1, Name = "امیر" },
        ];

        var expenses = new List<Expense>();
        var shares = new List<ExpenseShare>();
        for (var i = 1; i <= expenseCount; i++)
        {
            expenses.Add(new Expense
            {
                Id = i,
                TripId = 1,
                Description = $"هزینه {i}",
                Amount = 100_000,
                PaidById = 1,
                DateUtc = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(i),
            });
            shares.Add(new ExpenseShare { ExpenseId = i, ParticipantId = 1, Share = 50_000 });
            shares.Add(new ExpenseShare { ExpenseId = i, ParticipantId = 2, Share = 50_000 });
        }

        return (people, expenses, shares);
    }

    private static (string Html, int PageCount) Render(int expenseCount)
    {
        var (people, expenses, shares) = Trip(expenseCount);
        return ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);
    }

    [Fact]
    public void SmallTrip_FitsOnOnePage()
    {
        var (html, pages) = Render(1);

        Assert.Equal(1, pages);
        Assert.Equal(1, CountPages(html));
    }

    [Fact]
    public void Matrix_GivesEveryPersonAColumnAndEveryExpenseARow()
    {
        var (people, expenses, shares) = Trip(3);
        expenses[0].Description = "رستوران";

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        Assert.Contains("سهم هر نفر از هر هزینه", html);
        foreach (var person in people)
            Assert.Contains($">{person.Name}</th>", html);
        Assert.Contains("مبلغ کل", html);
        Assert.Contains("مجموع سهم", html);
    }

    [Fact]
    public void Matrix_MarksANonParticipantWithADashRatherThanAZero()
    {
        var (people, expenses, shares) = Trip(1);
        // Only person 1 shares this expense; person 2 sat it out.
        shares.RemoveAll(s => s.ParticipantId == 2);

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        Assert.Contains("—", html);
    }

    [Fact]
    public void Matrix_TintsThePayersOwnCell()
    {
        var (people, expenses, shares) = Trip(1);

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        Assert.Contains("num payer", html);
    }

    [Fact]
    public void Matrix_SplitsIntoGroupsRatherThanShrinkingColumns()
    {
        var people = Enumerable.Range(1, 7)
            .Select(i => new Participant { Id = i, TripId = 1, Name = $"P{i}" })
            .ToList();
        var (_, expenses, shares) = Trip(2);

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        // 7 people => groups of 5 and 2, so the matrix header appears twice.
        Assert.Equal(2, Occurrences(html, "مبلغ کل"));
        Assert.Equal(2, Occurrences(html, "مجموع سهم"));
    }

    [Fact]
    public void SettlementsStayOutOfTheMatrix()
    {
        var (people, expenses, shares) = Trip(1);
        expenses.Add(new Expense
        {
            Id = 99, TripId = 1, Description = "بازپرداخت", Amount = 50_000,
            PaidById = 2, IsSettlement = true,
            DateUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
        });
        shares.Add(new ExpenseShare { ExpenseId = 99, ParticipantId = 1, Share = 50_000 });

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        // Only matrix rows carry a .desc cell, so counting them counts the grid's rows:
        // the one real expense, never the settlement.
        Assert.Equal(1, Occurrences(html, "<span class=\"desc\">"));
        Assert.Contains("تسویه‌های ثبت‌شده", html);
    }

    [Fact]
    public void LongTrip_SpillsOntoMorePages()
    {
        var (html, pages) = Render(60);

        Assert.True(pages > 1);
        Assert.Equal(pages, CountPages(html));
    }

    [Fact]
    public void PageCount_MatchesTheSectionsTheWriterWillDraw()
    {
        // The PDF writer sizes the canvas from this number, so a mismatch would
        // silently produce blank or truncated pages.
        foreach (var count in new[] { 1, 12, 25, 40, 100 })
        {
            var (html, pages) = Render(count);
            Assert.Equal(pages, CountPages(html));
        }
    }

    [Fact]
    public void ContinuedTable_RepeatsItsColumnHeadings()
    {
        var (html, pages) = Render(60);

        Assert.True(pages > 1);
        // One heading per section plus one for every page the expense table continues onto.
        Assert.True(Occurrences(html, "پرداخت‌کننده") > 1);
    }

    [Fact]
    public void EveryPageCarriesItsOwnFooter()
    {
        var (html, pages) = Render(60);

        for (var i = 1; i <= pages; i++)
            Assert.Contains($"صفحهٔ {i} از {pages}", html);
    }

    [Fact]
    public void NamesAndDescriptionsAreHtmlEscaped()
    {
        List<Participant> people = [new() { Id = 1, TripId = 1, Name = "<script>" }];
        var (_, expenses, shares) = Trip(1);

        var (html, _) = ReportHtmlFormatter.Format("a & b", people, expenses, shares, Generated);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("a &amp; b", html);
    }

    [Fact]
    public void SectionHeading_IsNeverTheLastThingOnAPage()
    {
        // A heading reserves room for the table head and first row that follow it. The matrix
        // head used to re-check that for itself and ask for one row more, so it could fail on a
        // page its own heading had already passed on - stranding the heading above a blank gap.
        for (var count = 1; count <= 60; count++)
        {
            var (html, _) = Render(count);

            foreach (var body in PageBodies(html))
                Assert.False(
                    body.TrimEnd().EndsWith("</h2>", StringComparison.Ordinal),
                    $"a section heading ended a page in a report of {count} expenses");
        }
    }

    /// <summary>The drawn content of each page, with the repeated footer stripped off.</summary>
    private static IEnumerable<string> PageBodies(string html)
    {
        const string footer = "<div class=\"footer\">";
        foreach (var chunk in html.Split("<div class=\"content\">").Skip(1))
        {
            var end = chunk.IndexOf(footer, StringComparison.Ordinal);
            var body = end < 0 ? chunk : chunk[..end];
            yield return body.TrimEnd().EndsWith("</div>", StringComparison.Ordinal)
                ? body.TrimEnd()[..^"</div>".Length]
                : body;
        }
    }

    private static int CountPages(string html) => Occurrences(html, "<section class=\"page\">");

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
