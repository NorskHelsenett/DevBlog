using System.Text.Json.Serialization;

namespace AddressWebApi.Dtos;

// [JsonPropertyName("CadastreRoadAddress")]
public record CadastreRoadAddress
{
    public string? AddressId { get; set; }
    public string? AddressUuid { get; set; }
    public string? AddressCode { get; set; }
    public string? AddressType { get; set; }
    public string? UpdateDate { get; set; }
    public string? MunicipalityNumber { get; set; }
    public string? MunicipalityName { get; set; }
    public string? CadastralUnitNumber { get; set; }
    public string? PropertyUnitNumber { get; set; }
    public string? LeaseNumber { get; set; }
    public string? SubNumber { get; set; }
    public string? AddressAdditionalName { get; set; }
    public string? AddressName { get; set; }
    public string? Number { get; set; }
    public string? Letter { get; set; }
    public string? AddressText { get; set; }
    public string? AddressTextWithoutAddressAdditionalName { get; set; }
    public string? PostalCode { get; set; }
    public string? PostalCity { get; set; }
    public string? EpsgCode { get; set; }
    public string? North { get; set; }
    public string? East { get; set; }
    public string? AccessId { get; set; }
    public string? AccessUuid { get; set; }
    public string? AccessNorth { get; set; }
    public string? AccessSouth { get; set; }
    public string? AccessSummerId { get; set; }
    public string? AccessSummerUuid { get; set; }
    public string? AccessSummerNorth { get; set; }
    public string? AccessSummerEast { get; set; }
    public string? AccessWinterId { get; set; }
    public string? AccessWinterUuid { get; set; }
    public string? AccessWinterNorth { get; set; }
    public string? AccessWinterEast { get; set; }
}
