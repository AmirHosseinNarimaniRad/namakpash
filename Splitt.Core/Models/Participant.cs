using SQLite;

namespace Splitt.Core.Models;

[Table("Participant")]
public class Participant
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TripId { get; set; }

    public string Name { get; set; } = "";
}
