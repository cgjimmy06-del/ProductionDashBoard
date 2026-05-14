using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

var configPath = builder.Configuration["StoragePath"];
var webRoot = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var storagePath = string.IsNullOrEmpty(configPath)
    ? Path.Combine(webRoot, "logs")
    : configPath;
Directory.CreateDirectory(storagePath);

var fileProvider = new PhysicalFileProvider(storagePath);

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

app.MapPost("/api/logs/upload", async (IFormFile file) =>
{
    var safeName = Path.GetFileName(file.FileName);
    var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}";
    var filePath = Path.Combine(storagePath, fileName);

    await using var stream = File.Create(filePath);
    await file.CopyToAsync(stream);

    return Results.Ok(new { fileName });
}).DisableAntiforgery();

app.MapGet("/api/logs", () =>
{
    if (!Directory.Exists(storagePath))
        return Results.Ok(Array.Empty<object>());

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
