using System.IO;

namespace FProductionDashBoard.Services.WebApi;

public interface ILogUploadService
{
    Task<string> UploadLogFileAsync(string filePath);
    Task<string> UploadLogStreamAsync(Stream stream, string fileName);
}
