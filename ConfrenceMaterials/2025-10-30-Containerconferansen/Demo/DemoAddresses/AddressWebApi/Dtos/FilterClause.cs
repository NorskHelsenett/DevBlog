namespace AddressWebApi.Dtos;

public record FilterClause
{
    public FilterableField Field { get; set; }
    public string? Value { get; set; }
    public FilterCriteria Criteria { get; set; }
}
