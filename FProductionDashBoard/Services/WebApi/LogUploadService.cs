using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FProductionDashBoard.Services.WebApi;

public class LogUploadService : ILogUploadService
{
    private readonly HttpClient _http;

    public LogUploadService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> UploadLogFileAsync(string filePath)
    {
        await using var fileStream = File.OpenRead(filePath);
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _http.PostAsync("api/logs/upload", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
        return result?.FileName ?? string.Empty;
    }

    public async Task<string> UploadLogStreamAsync(Stream stream, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync("api/logs/upload", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
        return result?.FileName ?? string.Empty;
    }

    private record UploadResult(string FileName);
}
