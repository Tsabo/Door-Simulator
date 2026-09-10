namespace DoorSim.Data.Entities;

public class CardEntity
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public ushort FacilityCode { get; set; }

    public uint CardNumber { get; set; }

    public WiegandFormat Format { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CardEntry ToDto() => new(Id, Label, FacilityCode, CardNumber, Format, CreatedAt);

    public static CardEntity FromDto(CardEntry dto) => new()
    {
        Id = dto.Id,
        Label = dto.Label,
        FacilityCode = dto.FacilityCode,
        CardNumber = dto.CardNumber,
        Format = dto.Format,
        CreatedAt = dto.CreatedAt
    };
}
