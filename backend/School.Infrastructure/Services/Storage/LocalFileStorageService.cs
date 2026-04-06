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

        string uploadPath = Path.Combine(_env.WebRootPath, "uploads", folderName);
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        string fileName = $"{DateTime.UtcNow.Ticks}_{file.FileName}";
        string filePath = Path.Combine(uploadPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Return the relative URL
        return $"/uploads/{folderName}/{fileName}";
    }

    public Task<bool> DeleteFileAsync(string fileUrl)
    {
        try 
        {
            // Convert URL to physical path
            string relativePath = fileUrl.TrimStart('/');
            string physicalPath = Path.Combine(_env.WebRootPath, relativePath);
            
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                return Task.FromResult(true);
            }
        }
        catch { }
        
        return Task.FromResult(false);
    }
}
