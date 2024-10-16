using System.Security.Claims;
using fileshare_web.Components;

namespace fileshare_web;

public class KafkaFileApiService(HttpClient httpClient, ILogger<KafkaFileApiService> logger)
{
    public async Task<Stream> RetrievFile(string accessToken, string fileName, string owner)
    {

        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/retrieve";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Headers.Add("X-Blob-Name", fileName);
        request.Headers.Add("X-Owner-id", owner);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await response.Content.ReadAsStreamAsync();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "\n\nRequest to external service failed");
            throw;
        }
        throw new Exception("Failed to get file");
    }
    public async Task<bool> DeleteFile(string accessToken, string fileName)
    {

        var address = $"{Environment.GetEnvironmentVariable("FILESHARE_WEB_REMOTE_FILE_API_ADDRESS")}/remove";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address));
        request.Headers.Add("X-Blob-Name", fileName);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception e)
        {
            logger.LogError(e, "\n\nRequest to external service failed");

        }

        return false;
    }
    public async Task<bool> SaveFile(string accessToken, Stream fileToSave, string fileName)
    {
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
        catch (Exception e)
        {
            logger.LogError(e, "\n\nRequest to external service failed");

        }

        return false;
    }



}
