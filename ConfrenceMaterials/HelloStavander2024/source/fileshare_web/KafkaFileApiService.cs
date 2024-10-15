using System.Security.Claims;
using fileshare_web.Components;

namespace fileshare_web;

public class KafkaFileApiService(HttpClient httpClient, HttpContextAccessor httpContextAccessor, ILogger<KafkaFileApiService> logger)
{
    public async Task<bool> DeleteFile(string accessToken, string fileName){

        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/remove";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Headers.Add("X-Blob-Name", fileName);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception e) {
            logger.LogError(e,"\n\nRequest to external service failed");

        }

        return false;
    }
    public async Task<bool> SaveFile(string accessToken, Stream fileToSave, string fileName){
        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/store";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Content = new StreamContent(fileToSave);
        request.Headers.Add("X-Blob-Name", fileName);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            logger.LogDebug($"Sending request to Kafka API to store file with name {fileName}");
            var response = await httpClient.SendAsync(request);
            logger.LogDebug($"Received response from Kafka API to store file with name {fileName}");
            logger.LogTrace($"Response was {response}");
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception e) {
            logger.LogError(e,"\n\nRequest to external service failed");

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
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var responseString = response.Content.ReadAsStringAsync().Result;
                logger.LogDebug(responseString);
                if (string.IsNullOrWhiteSpace(responseString))
                {
                    return [];
                }
                var responseItems = responseString.Split("\\n");

                return responseItems.Select(MappToSecretFile).ToList();
            }
        }
        catch (Exception e) {

            logger.LogError(e,"\n\nRequest to external service failed");

        }

        return [];
    }

    private SecretFile MappToSecretFile(string item)
    {
        var itemParts = item.Split("\\t");
        return new SecretFile{
            Name = itemParts[1],
            Rights = GetRights(itemParts[0])
        };
    }

    private FileRights GetRights(string fileOwnerName)
    {

        var userId = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        FileRights rights = 0;
        logger.LogDebug($"what are we comparing; {fileOwnerName} and {userId}");
        if(string.Equals(fileOwnerName,userId)) rights = FileRights.Owner | rights;

        return rights;
    }
}
