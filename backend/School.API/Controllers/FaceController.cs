using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.Interfaces;

namespace School.API.Controllers;

[Authorize(Roles = "Admin,Teacher")]
public class FaceController : BaseApiController
{
    private readonly IFaceRecognitionService _faceRecognitionService;

    public FaceController(IFaceRecognitionService faceRecognitionService)
    {
        _faceRecognitionService = faceRecognitionService;
    }

    [HttpPost("train/{studentId}")]
    public async Task<ActionResult> TrainFace(int studentId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Image is required" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var result = await _faceRecognitionService.TrainFaceAsync(studentId, ms.ToArray(), file.FileName);
        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Face trained successfully"
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Training failed"
        });
    }
}
