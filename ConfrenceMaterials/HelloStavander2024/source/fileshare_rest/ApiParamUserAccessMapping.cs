public record ApiParamUserAccessMapping
{
    public required string BlobName { get; init; }
    public required string Owner { get; init; }
    public required string[] CanChangeAccess { get; init; }
    public required string[] CanRetrieve { get; init; }
    public required string[] CanChange { get; init; }
    public required string[] CanDelete { get; init; }
}
