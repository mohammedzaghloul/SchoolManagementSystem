using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using School.Application.Interfaces;

namespace School.Infrastructure.Services.Storage;

public class GoogleDriveStorageService : IFileStorageService
{
    private readonly IConfiguration _config;
    private readonly string[] _scopes = { DriveService.Scope.DriveFile };
    private readonly string _applicationName = "SchoolManagementSystem";

    public GoogleDriveStorageService(IConfiguration config)
    {
        _config = config;
    }

    private async Task<DriveService> GetDriveServiceAsync()
    {
        // 1. Get credentials from appsettings.json or a JSON file path
        // For a Service Account, we usually expect a JSON key path
        string? googleCredentialsPath = _config["GoogleDrive:CredentialsPath"];
        
        if (string.IsNullOrEmpty(googleCredentialsPath) || !File.Exists(googleCredentialsPath))
        {
            throw new Exception("Google Drive credentials not found. Please provide a valid path in appsettings.json.");
        }

        GoogleCredential credential;
        using (var stream = new FileStream(googleCredentialsPath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(_scopes);
        }

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        var service = await GetDriveServiceAsync();
        
        // 1. Check if folder exists or create it
        string folderId = await GetOrCreateFolderAsync(service, folderName);

        // 2. Prepare file metadata
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = $"{DateTime.UtcNow.Ticks}_{file.FileName}",
            Parents = new List<string> { folderId }
        };

        // 3. Upload the file
        FilesResource.CreateMediaUpload request;
        using (var stream = file.OpenReadStream())
        {
            request = service.Files.Create(fileMetadata, stream, file.ContentType);
            request.Fields = "id, webViewLink, webContentLink";
            await request.UploadAsync();
        }

        var uploadedFile = request.ResponseBody;
        
        // Note: For public access, you'd need to set permissions here
        // return uploadedFile.WebViewLink; 
        return uploadedFile.Id; // Returning ID for internal use, or link
    }

    private async Task<string> GetOrCreateFolderAsync(DriveService service, string folderName)
    {
        var request = service.Files.List();
        request.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and trashed = false";
        var result = await request.ExecuteAsync();
        
        var folder = result.Files.FirstOrDefault();
        if (folder != null) return folder.Id;

        // Create new folder
        var folderMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };
        
        var newFolder = await service.Files.Create(folderMetadata).ExecuteAsync();
        return newFolder.Id;
    }

    public async Task<bool> DeleteFileAsync(string fileId)
    {
        var service = await GetDriveServiceAsync();
        try 
        {
            await service.Files.Delete(fileId).ExecuteAsync();
            return true;
        }
        catch 
        {
            return false;
        }
    }
}
