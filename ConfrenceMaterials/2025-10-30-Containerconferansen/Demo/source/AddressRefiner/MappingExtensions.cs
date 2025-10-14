using No.Nhn.Address.Cadastre.ImportFormat;
using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

public static class MappingExtensions
{
    public static byte[] GetUtf8Bytes(this string input)
    {
        return System.Text.Encoding.UTF8.GetBytes(input);
    }

    public static string GetUtf8String(this byte[] input)
    {
        return System.Text.Encoding.UTF8.GetString(input);
    }

    public static bool ValueEquals(this CadastreRoadAddress first, CadastreRoadAddress second)
    {
        return first.AddressId == second.AddressId
               && first.AddressUuid == second.AddressUuid
               && first.AddressCode == second.AddressCode
               && first.AddressType == second.AddressType
               && first.UpdateDate == second.UpdateDate
               && first.MunicipalityNumber == second.MunicipalityNumber
               && first.MunicipalityName == second.MunicipalityName
               && first.CadastralUnitNumber == second.CadastralUnitNumber
               && first.PropertyUnitNumber == second.PropertyUnitNumber
               && first.LeaseNumber == second.LeaseNumber
               && first.SubNumber == second.SubNumber
               && first.AddressAdditionalName == second.AddressAdditionalName
               && first.AddressName == second.AddressName
               && first.Number == second.Number
               && first.Letter == second.Letter
               && first.AddressText == second.AddressText
               && first.AddressTextWithoutAddressAdditionalName == second.AddressTextWithoutAddressAdditionalName
               && first.PostalCode == second.PostalCode
               && first.PostalCity == second.PostalCity
               && first.EpsgCode == second.EpsgCode
               && first.North == second.North
               && first.East == second.East
               && first.AccessId == second.AccessId
               && first.AccessUuid == second.AccessUuid
               && first.AccessNorth == second.AccessNorth
               && first.AccessSouth == second.AccessSouth
               && first.AccessSummerId == second.AccessSummerId
               && first.AccessSummerUuid == second.AccessSummerUuid
               && first.AccessSummerNorth == second.AccessSummerNorth
               && first.AccessSummerEast == second.AccessSummerEast
               && first.AccessWinterId == second.AccessWinterId
               && first.AccessWinterUuid == second.AccessWinterUuid
               && first.AccessWinterNorth == second.AccessWinterNorth
               && first.AccessWinterEast == second.AccessWinterEast;
    }

    public static CadastreRoadAddress ToCadastreRoadAddress(this CadastreRoadAddressImport source)
    {
        return new CadastreRoadAddress
        {
            AddressId = source.AddressId,
            AddressUuid = source.UuidAddress,
            AddressCode = source.AddressCode,
            AddressType = source.AddressType,
            UpdateDate = source.UpdateDate,
            MunicipalityNumber = source.MunicipalityNumber,
            MunicipalityName = source.MunicipalityName,
            CadastralUnitNumber = source.CadastralUnitNumber,
            PropertyUnitNumber = source.PropertyUnitNumber,
            LeaseNumber = source.LeaseNumber,
            SubNumber = source.SubNumber,
            AddressAdditionalName = source.AddressAdditionalName,
            AddressName = source.AddressName,
            Number = source.Number,
            Letter = source.Letter,
            AddressText = source.AddressText,
            AddressTextWithoutAddressAdditionalName = source.AddressTextWithoutAddressAdditionalName,
            PostalCode = source.PostalCode,
            PostalCity = source.PostalCity,
            EpsgCode = source.EpsgCode,
            North = source.North,
            East = source.East,
            AccessId = source.AccessId,
            AccessUuid = source.UuidAccess,
            AccessNorth = source.AccessNorth,
            AccessSouth = source.AccessSouth,
            AccessSummerId = source.SummerAccessId,
            AccessSummerUuid = source.UuidSummerAccess,
            AccessSummerNorth = source.SummerAccessNorth,
            AccessSummerEast = source.SummerAccessEast,
            AccessWinterId = source.WinterAccessId,
            AccessWinterUuid = source.UuidWinterAccess,
            AccessWinterNorth = source.WinterAccessNorth,
            AccessWinterEast = source.WinterAccessEast,
        };
    }
}
