using Microsoft.AspNetCore.Http;

namespace School.Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file and returns the accessible URL
    /// </summary>
    Task<string> UploadFileAsync(IFormFile file, string folderName);
    
    /// <summary>
    /// Deletes a file from storage
    /// </summary>
    Task<bool> DeleteFileAsync(string fileUrl);
}
