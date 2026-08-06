using System.Globalization;
using SQLite;

namespace Splitt.Core.Models;

[Table("Expense")]
public class Expense
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TripId { get; set; }

    public string Description { get; set; } = "";

    // Money must never touch REAL/double: persisted as invariant TEXT.
    public string AmountRaw { get; set; } = "0";

    [Ignore]
    public decimal Amount
    {
        get => decimal.Parse(AmountRaw, CultureInfo.InvariantCulture);
        set => AmountRaw = value.ToString(CultureInfo.InvariantCulture);
    }

    public int PaidById { get; set; }

    public DateTime DateUtc { get; set; }

    public bool IsSettlement { get; set; }
}
