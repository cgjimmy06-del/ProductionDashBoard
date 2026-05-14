using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDirectoryBrowser();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var configPath = builder.Configuration["StoragePath"];
// WebRootPath is null when wwwroot directory does not exist (e.g. fresh clone or IIS deploy)
var webRoot = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var storagePath = string.IsNullOrEmpty(configPath)
    ? Path.Combine(webRoot, "logs")
    : configPath;
Directory.CreateDirectory(storagePath);

var fileProvider = new PhysicalFileProvider(storagePath);

app.UseSwagger();
app.UseSwaggerUI();

// Serve uploaded files as static content and enable directory listing for browser access
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider,
    RequestPath = "/logs"
});
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = fileProvider,
    RequestPath = "/logs"
});

const long maxFileSize = 50 * 1024 * 1024; // 50 MB — prevent disk exhaustion from oversized uploads

app.MapPost("/api/logs/upload", async (IFormFile file) =>
{
    if (file.Length > maxFileSize)
        return Results.BadRequest($"File exceeds the {maxFileSize / 1024 / 1024} MB limit.");

    try
    {
        // Path.GetFileName strips directory traversal attempts (e.g. "../../evil.exe")
        var safeName = Path.GetFileName(file.FileName);
        // Timestamp prefix avoids name collisions when the same file is uploaded multiple times
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}";
        var filePath = Path.Combine(storagePath, fileName);

        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);

        return Results.Ok(new { fileName });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
// DisableAntiforgery is required for multipart/form-data uploads from non-browser clients
}).DisableAntiforgery();

app.MapGet("/api/logs", () =>
{
    var files = Directory.GetFiles(storagePath)
        .Select(f =>
        {
            var info = new FileInfo(f);
            return new { name = info.Name, size = info.Length, lastModified = info.LastWriteTime };
        })
        .OrderByDescending(f => f.lastModified);

    return Results.Ok(files);
});

app.Run();
