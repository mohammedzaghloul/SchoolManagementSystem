using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using School.Application.Interfaces;

namespace School.Infrastructure.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0) return "";

        var webRootPath = EnsureWebRootPath();
        var uploadPath = Path.Combine(webRootPath, "uploads", folderName);
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var safeFileName = Path.GetFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow.Ticks}_{safeFileName}";
        var filePath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{folderName}/{fileName}";
    }

    public Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.TrimStart('/');
            var physicalPath = Path.Combine(EnsureWebRootPath(), relativePath);

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                return Task.FromResult(true);
            }
        }
        catch
        {
        }

        return Task.FromResult(false);
    }

    private string EnsureWebRootPath()
    {
        var webRootPath = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
        }

        if (!Directory.Exists(webRootPath))
        {
            Directory.CreateDirectory(webRootPath);
        }

        return webRootPath;
    }
}
