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
    public void EveryPersonGetsTheirShareItemisedWithDateAndTime()
    {
        var (people, expenses, shares) = Trip(2);
        expenses[0].Description = "رستوران";
        expenses[0].DateUtc = new DateTime(2026, 8, 2, 9, 30, 0, DateTimeKind.Utc);

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        Assert.Contains("ریز سهم هر نفر", html);
        // Both sharers are listed, and the expense appears once under each of them.
        foreach (var person in people)
            Assert.Contains(person.Name, html);
        Assert.True(Occurrences(html, "رستوران") >= people.Count);

        var local = expenses[0].DateUtc.ToLocalTime();
        Assert.Contains(local.ToString("HH:mm"), html);
    }

    [Fact]
    public void SettlementsStayOutOfThePerPersonBreakdown()
    {
        var (people, expenses, shares) = Trip(1);
        expenses.Add(new Expense
        {
            Id = 99, TripId = 1, Description = "تسویه", Amount = 50_000,
            PaidById = 2, IsSettlement = true,
            DateUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
        });
        shares.Add(new ExpenseShare { ExpenseId = 99, ParticipantId = 1, Share = 50_000 });

        var (html, _) = ReportHtmlFormatter.Format("سفر", people, expenses, shares, Generated);

        // "تسویه" belongs in its own section, not in anyone's list of what they owe.
        Assert.Equal(1, Occurrences(html, "تسویه‌های ثبت‌شده"));
        Assert.DoesNotContain("<td class=\"r\" width=\"44%\">تسویه</td>", html);
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
