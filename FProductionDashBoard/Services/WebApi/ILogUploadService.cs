namespace FProductionDashBoard.Services.WebApi;

public interface ILogUploadService
{
    Task<string> UploadLogFileAsync(string filePath);
}
