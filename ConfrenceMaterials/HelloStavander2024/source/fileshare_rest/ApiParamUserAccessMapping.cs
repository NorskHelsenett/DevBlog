public record ApiParamUserAccessMapping
{
    public required string BlobName { get; init; }
    public required string[] OwnerIds { get; init; }
    public required string[] CanRetrieve { get; init; }
    public required string[] CanChange { get; init; }
    public required string[] CanDelete { get; init; }
}
