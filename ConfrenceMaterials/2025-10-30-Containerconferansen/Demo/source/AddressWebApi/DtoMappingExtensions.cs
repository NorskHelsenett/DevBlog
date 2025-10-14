using AddressWebApi.Dtos;

namespace AddressWebApi;

/// <summary>
/// Class for mapping DTO object content, like filednames, to fixed strings known ahead of time, for safer usage on the backed.
/// </summary>
public static class DtoMappingExtensions
{
    public static string FieldName(this RequestableField requestableField)
    {
        return requestableField switch
        {
            RequestableField.AddressId => "AddressId",
            RequestableField.AddressUuid => "AddressUuid",
            RequestableField.AddressCode => "AddressCode",
            RequestableField.AddressType => "AddressType",
            RequestableField.UpdateDate => "UpdateDate",
            RequestableField.MunicipalityNumber => "MunicipalityNumber",
            RequestableField.MunicipalityName => "MunicipalityName",
            RequestableField.CadastralUnitNumber => "CadastralUnitNumber",
            RequestableField.PropertyUnitNumber => "PropertyUnitNumber",
            RequestableField.LeaseNumber => "LeaseNumber",
            RequestableField.SubNumber => "SubNumber",
            RequestableField.AddressAdditionalName => "AddressAdditionalName",
            RequestableField.AddressName => "AddressName",
            RequestableField.Number => "Number",
            RequestableField.Letter => "Letter",
            RequestableField.AddressText => "AddressText",
            RequestableField.AddressTextWithoutAddressAdditionalName => "AddressTextWithoutAddressAdditionalName",
            RequestableField.PostalCode => "PostalCode",
            RequestableField.PostalCity => "PostalCity",
            RequestableField.EpsgCode => "EpsgCode",
            RequestableField.North => "North",
            RequestableField.East => "East",
            RequestableField.AccessId => "AccessId",
            RequestableField.AccessUuid => "AccessUuid",
            RequestableField.AccessNorth => "AccessNorth",
            RequestableField.AccessSouth => "AccessSouth",
            RequestableField.AccessSummerId => "AccessSummerId",
            RequestableField.AccessSummerUuid => "AccessSummerUuid",
            RequestableField.AccessSummerNorth => "AccessSummerNorth",
            RequestableField.AccessSummerEast => "AccessSummerEast",
            RequestableField.AccessWinterId => "AccessWinterId",
            RequestableField.AccessWinterUuid => "AccessWinterUuid",
            RequestableField.AccessWinterNorth => "AccessWinterNorth",
            RequestableField.AccessWinterEast => "AccessWinterEast",
            _ => throw new ArgumentOutOfRangeException(nameof(requestableField), requestableField, null)
        };
    }

    public static string FieldName(this FilterableField filterableField)
    {
        return filterableField switch
        {
            FilterableField.AddressId => "AddressId",
            FilterableField.AddressUuid => "AddressUuid",
            FilterableField.AddressCode => "AddressCode",
            FilterableField.AddressType => "AddressType",
            FilterableField.UpdateDate => "UpdateDate",
            FilterableField.MunicipalityNumber => "MunicipalityNumber",
            FilterableField.MunicipalityName => "MunicipalityName",
            FilterableField.CadastralUnitNumber => "CadastralUnitNumber",
            FilterableField.PropertyUnitNumber => "PropertyUnitNumber",
            FilterableField.LeaseNumber => "LeaseNumber",
            FilterableField.SubNumber => "SubNumber",
            FilterableField.AddressAdditionalName => "AddressAdditionalName",
            FilterableField.AddressName => "AddressName",
            FilterableField.Number => "Number",
            FilterableField.Letter => "Letter",
            FilterableField.AddressText => "AddressText",
            FilterableField.AddressTextWithoutAddressAdditionalName => "AddressTextWithoutAddressAdditionalName",
            FilterableField.PostalCode => "PostalCode",
            FilterableField.PostalCity => "PostalCity",
            FilterableField.EpsgCode => "EpsgCode",
            FilterableField.North => "North",
            FilterableField.East => "East",
            FilterableField.AccessId => "AccessId",
            FilterableField.AccessUuid => "AccessUuid",
            FilterableField.AccessNorth => "AccessNorth",
            FilterableField.AccessSouth => "AccessSouth",
            FilterableField.AccessSummerId => "AccessSummerId",
            FilterableField.AccessSummerUuid => "AccessSummerUuid",
            FilterableField.AccessSummerNorth => "AccessSummerNorth",
            FilterableField.AccessSummerEast => "AccessSummerEast",
            FilterableField.AccessWinterId => "AccessWinterId",
            FilterableField.AccessWinterUuid => "AccessWinterUuid",
            FilterableField.AccessWinterNorth => "AccessWinterNorth",
            FilterableField.AccessWinterEast => "AccessWinterEast",
            _ => throw new ArgumentOutOfRangeException(nameof(filterableField), filterableField, null)
        };
    }
}
