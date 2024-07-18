public class ExternalInvokerService(HttpClient httpClient)
{
    public async Task<string> PokeExternalService(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://host.docker.internal:3001"));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
            // return $"Stack: {e.StackTrace}"; // Don't do this at home
        }

        return "Fetch failed";
    }
}