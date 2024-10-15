public record ApiParamUserAccessMapping
{
    public required string BlobName { get; init; }
    public required string Owner { get; init; }
    public required List<string> CanChangeAccess { get; set; }
    public required List<string> CanRetrieve { get; set; }
    public required List<string> CanChange { get; set; }
    public required List<string> CanDelete { get; set; }
}
