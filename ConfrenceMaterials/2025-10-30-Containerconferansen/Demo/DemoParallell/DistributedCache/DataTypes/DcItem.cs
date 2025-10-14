public record DcItem
{
    public required string Key { get; init; }
    public byte[]? Value { get; init; }
    public List<KeyValuePair<string, string>>? Headers { get; set; }
}
