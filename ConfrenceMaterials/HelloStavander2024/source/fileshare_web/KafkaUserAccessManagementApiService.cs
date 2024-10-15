public class KafkaUserAccessManagementApiService(HttpClient httpClient, ILogger<KafkaUserAccessManagementApiService> logger)
{
    public async Task<List<ApiParamUserAccessMapping>> GetUserAccessMappings(string accessToken)
    {
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/userAccessMappings";
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseParsed = System.Text.Json.JsonSerializer.Deserialize<List<ApiParamUserAccessMapping>>(responseString);
            return responseParsed ?? [];
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
            // return $"Stack: {e.StackTrace}"; // Don't do this at home
        }

        return [];
    }

    public async Task RegisterUserAccessMappings(string accessToken, ApiParamUserAccessMapping userAccessMapping)
    {
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/updateUserAccessMapping";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(userAccessMapping);
        try
        {
            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug(responseString);
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
            // return $"Stack: {e.StackTrace}"; // Don't do this at home
        }
    }
}
//updateUserAccessMapping
// GET {{baseurl}}/userAccessMappings
