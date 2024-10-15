using System.Security.Claims;
using fileshare_web.Components;

namespace fileshare_web;

public class KafkaFileApiService(HttpClient httpClient, HttpContextAccessor httpContextAccessor, ILogger<KafkaFileApiService> logger)
{
    public async Task<bool> SaveFile(string accessToken, Stream fileToSave){
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/store";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Content = new StreamContent(fileToSave);
        request.Headers.Add("X-Blob-Name", "testing");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
        }

        return false;
    }
    public async Task<List<SecretFile>> GetListOfFiles(string accessToken)
    {
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/list";
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = httpClient.SendAsync(request).Result;
            var responseString = response.Content.ReadAsStringAsync().Result;
            logger.LogDebug(responseString);
            var responseItems = responseString.Split("\n");
            
            return responseItems.Select(MappToSecretFile).ToList();
        }
        catch (Exception e) {
            Console.WriteLine("\n\nRequest to external service failed");
            Console.WriteLine(e);
        }

        return [];
    }

    private SecretFile MappToSecretFile(string item)
    {
        var itemParts = item.Split("\t");
        return new SecretFile{
            Name = itemParts[0],
            Rights = GetRights(itemParts[1])
        };
    }

    private FileRights GetRights(string fileOwnerName)
    {
        
        var userId = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        FileRights rights = 0;
        if(string.Equals(fileOwnerName,userId)) rights = FileRights.Owner | rights;

        return rights;
    }
}
