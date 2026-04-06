using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using School.Application.Interfaces;

namespace School.Infrastructure.Services.Storage;

public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(IConfiguration config)
    {
        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0) return "";

        var uploadParams = new RawUploadParams()
        {
            File = new FileDescription(file.FileName, file.OpenReadStream()),
            Folder = $"SchoolPortal/{folderName}",
            DisplayName = file.FileName
        };

        // If it's an image, we can use ImageUploadParams for optimizations
        if (file.ContentType.StartsWith("image/"))
        {
            var imageParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                Folder = $"SchoolPortal/{folderName}",
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };
            
            var imageResult = await _cloudinary.UploadAsync(imageParams);
            return imageResult.SecureUrl.ToString();
        }

        var result = await _cloudinary.UploadAsync(uploadParams);
        return result.SecureUrl.ToString();
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try 
        {
            // Extract public ID from URL (Cloudinary specific logic)
            // Example URL: https://res.cloudinary.com/cloudname/image/upload/v123/folder/public_id.jpg
            var uri = new Uri(fileUrl);
            var publicId = Path.GetFileNameWithoutExtension(uri.LocalPath);
            
            var deletionParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deletionParams);
            
            return result.Result == "ok";
        }
        catch 
        {
            return false;
        }
    }
}
