using System.Text.Json;
using System.Text.Json.Serialization;
using Splitt.Core.Models;

namespace Splitt.Core.Export;

public sealed class TripExportDto
{
    public int SchemaVersion { get; set; } = 1;
    public string TripName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public List<ParticipantDto> Participants { get; set; } = [];
    public List<ExpenseDto> Expenses { get; set; } = [];
}

public sealed class ParticipantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ExpenseDto
{
    public string Description { get; set; } = "";
    // Amounts travel as strings so no JSON reader can degrade them to double.
    public string Amount { get; set; } = "0";
    public int PaidById { get; set; }
    public DateTime DateUtc { get; set; }
    public bool IsSettlement { get; set; }
    public List<ShareDto> Shares { get; set; } = [];
}

public sealed class ShareDto
{
    public int ParticipantId { get; set; }
    public string Share { get; set; } = "0";
}

public static class TripExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(
        Trip trip,
        IEnumerable<Participant> participants,
        IEnumerable<Expense> expenses,
        IEnumerable<ExpenseShare> shares)
    {
        var sharesByExpense = shares.ToLookup(s => s.ExpenseId);

        var dto = new TripExportDto
        {
            TripName = trip.Name,
            CreatedAtUtc = trip.CreatedAtUtc,
            Participants = participants
                .Select(p => new ParticipantDto { Id = p.Id, Name = p.Name })
                .ToList(),
            Expenses = expenses
                .Select(e => new ExpenseDto
                {
                    Description = e.Description,
                    Amount = e.AmountRaw,
                    PaidById = e.PaidById,
                    DateUtc = e.DateUtc,
                    IsSettlement = e.IsSettlement,
                    Shares = sharesByExpense[e.Id]
                        .Select(s => new ShareDto { ParticipantId = s.ParticipantId, Share = s.ShareRaw })
                        .ToList(),
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    public static TripExportDto FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<TripExportDto>(json, Options)
            ?? throw new InvalidDataException("فایل پشتیبان معتبر نیست.");

        if (dto.Participants.Count == 0)
            throw new InvalidDataException("فایل پشتیبان هیچ شرکت‌کننده‌ای ندارد.");

        var ids = dto.Participants.Select(p => p.Id).ToHashSet();
        foreach (var e in dto.Expenses)
        {
            if (!ids.Contains(e.PaidById) || e.Shares.Any(s => !ids.Contains(s.ParticipantId)))
                throw new InvalidDataException("فایل پشتیبان ناسازگار است (شناسهٔ نامعتبر).");
            _ = decimal.Parse(e.Amount, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var s in e.Shares)
                _ = decimal.Parse(s.Share, System.Globalization.CultureInfo.InvariantCulture);
        }

        return dto;
    }
}
