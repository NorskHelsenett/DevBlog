public static class Mapping
{
    public static ApiParamPerson ToPersonJson(this Person personProtobuf)
    {
        return new ApiParamPerson
        {
            Id = personProtobuf.Id,
            Name = new ApiParamPersonName
            {
                Given = personProtobuf.Name.Given,
                Family = personProtobuf.Name.Family,
            },
            Tags = personProtobuf.Tags?.ToList(),
        };
    }

    public static Person ToPersonProtobuf(this ApiParamPerson personJson)
    {
        var result = new Person
        {
            Id = personJson.Id,
            Name = new PersonName
            {
                Given = personJson.Name.Given,
                Family = personJson.Name.Family,
            },
        };
        if(personJson.Tags?.Count != 0)
        {
            result.Tags.Add(personJson.Tags);
        }
        return result;
    }

    public static string SerializeExplicitlyForAot(this ApiParamPerson inputPerson)
    {
        return System.Text.Json.JsonSerializer.Serialize(inputPerson!, SourceGenerationContext.Default.ApiParamPerson);
    }

    public static byte[] GetUtf8Bytes(this string input)
    {
        return System.Text.Encoding.UTF8.GetBytes(input);
    }

    public static string GetUtf8String(this byte[] input)
    {
        return System.Text.Encoding.UTF8.GetString(input);
    }
}
