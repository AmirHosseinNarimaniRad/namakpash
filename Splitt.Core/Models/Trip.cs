using SQLite;

namespace Splitt.Core.Models;

[Table("Trip")]
public class Trip
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
}
