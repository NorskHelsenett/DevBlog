namespace AddressWebApi.Dtos;

public record Query
{
    public required List<FilterClause> Filters { get; set; }
    public required  List<RequestableField> RequestedFields { get; set; }
}
