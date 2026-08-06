using System.Globalization;
using SQLite;

namespace Splitt.Core.Models;

[Table("ExpenseShare")]
public class ExpenseShare
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ExpenseId { get; set; }

    [Indexed]
    public int ParticipantId { get; set; }

    // Money must never touch REAL/double: persisted as invariant TEXT.
    public string ShareRaw { get; set; } = "0";

    [Ignore]
    public decimal Share
    {
        get => decimal.Parse(ShareRaw, CultureInfo.InvariantCulture);
        set => ShareRaw = value.ToString(CultureInfo.InvariantCulture);
    }
}
