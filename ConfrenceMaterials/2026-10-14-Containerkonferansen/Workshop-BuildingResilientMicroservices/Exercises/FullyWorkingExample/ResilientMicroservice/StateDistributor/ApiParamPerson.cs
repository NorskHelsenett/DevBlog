using System.Text.Json.Serialization;

public record ApiParamPerson
{
    public required string Id { get; init; }
    public required ApiParamPersonName Name { get; init; }
    public List<string>? Tags { get; set; }
}

public record ApiParamPersonName
{
    public required string Given { get; init; }
    public required string Family { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ApiParamPerson))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
