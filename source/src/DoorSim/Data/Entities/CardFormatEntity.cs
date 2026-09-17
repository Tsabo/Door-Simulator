namespace DoorSim.Data.Entities;

public class CardFormatEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CardMask { get; set; } = string.Empty;

    public string? Parity1Mask { get; set; }

    public string? Parity2Mask { get; set; }

    public string? Parity3Mask { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CustomCardFormat ToDto() => new(Id, Name, CardMask, Parity1Mask, Parity2Mask, Parity3Mask, CreatedAt);

    public static CardFormatEntity FromDto(CustomCardFormat dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        CardMask = dto.CardMask,
        Parity1Mask = dto.Parity1Mask,
        Parity2Mask = dto.Parity2Mask,
        Parity3Mask = dto.Parity3Mask,
        CreatedAt = dto.CreatedAt
    };
}
