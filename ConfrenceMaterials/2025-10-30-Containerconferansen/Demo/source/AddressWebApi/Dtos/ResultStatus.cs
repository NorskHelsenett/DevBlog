namespace AddressWebApi.Dtos;

public record ResultStatus
{
    public required ResultStatusTypes Type { get; init; }
    public Dictionary<string,string>? AdditionalInfo { get; set; }
}
