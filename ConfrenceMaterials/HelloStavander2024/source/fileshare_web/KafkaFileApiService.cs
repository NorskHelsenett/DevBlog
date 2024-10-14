namespace fileshare_web;

public class KafkaFileApiService(HttpClient httpClient)
{
    public async Task<List<string>> GetListOfFiles(string accessToken)
    {
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/list";
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseItems = responseString.Split('\u001E');
            return responseItems.ToList();
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
            // return $"Stack: {e.StackTrace}"; // Don't do this at home
        }

        return [];
    }
}
