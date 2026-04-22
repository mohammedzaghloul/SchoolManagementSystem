using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using School.Application.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace School.Infrastructure.Services;

public class FaceRecognitionService : IFaceRecognitionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FaceRecognitionService> _logger;

    public FaceRecognitionService(HttpClient httpClient, IConfiguration config, ILogger<FaceRecognitionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = (config["FaceRecognition:BaseUrl"] ?? "http://localhost:8000").Trim().TrimEnd('/');
        _httpClient.BaseAddress = new Uri($"{baseUrl}/");
    }

    public async Task<FaceTrainingResult> TrainFaceAsync(int studentId, byte[] imageBytes, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(studentId.ToString()), "student_id");

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetContentType(fileName));
        content.Add(imageContent, "file", fileName);

        try
        {
            var response = await _httpClient.PostAsync("/train", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var message = TranslateFaceServiceMessage(ExtractServiceMessage(responseBody))
                    ?? "تعذر تسجيل الوجه. تأكد من وضوح الصورة أو من توفر خدمة التعرف على الوجه.";

                _logger.LogWarning(
                    "Face training failed for student {StudentId} with status code {StatusCode}. Detail: {Detail}",
                    studentId,
                    response.StatusCode,
                    responseBody);

                return new FaceTrainingResult
                {
                    Success = false,
                    Message = message
                };
            }

            var successPayload = DeserializeMessageResponse(responseBody);

            return new FaceTrainingResult
            {
                Success = true,
                Message = TranslateFaceServiceMessage(successPayload?.Message) ?? "تم تسجيل الوجه بنجاح."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face training service is unavailable for student {StudentId}.", studentId);
            return new FaceTrainingResult
            {
                Success = false,
                Message = "خدمة التعرف على الوجه غير متاحة حالياً. حاول مرة أخرى بعد قليل."
            };
        }
    }

    public async Task<FaceRecognitionResult> RecognizeFaceAsync(byte[] imageBytes, string fileName)
    {
        using var content = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetContentType(fileName));
        content.Add(imageContent, "file", fileName);

        try
        {
            var response = await _httpClient.PostAsync("/recognize", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Face recognition failed with status code {StatusCode}. Detail: {Detail}",
                    response.StatusCode,
                    responseBody);

                return new FaceRecognitionResult
                {
                    Success = false,
                    Message = TranslateFaceServiceMessage(ExtractServiceMessage(responseBody))
                };
            }

            var result = DeserializeRecognitionResponse(responseBody);
            if (result != null && result.Recognized.Any())
            {
                var bestMatch = result.Recognized.OrderByDescending(r => r.Confidence).First();
                return new FaceRecognitionResult
                {
                    Success = true,
                    StudentId = bestMatch.StudentId,
                    Confidence = bestMatch.Confidence,
                    Message = TranslateFaceServiceMessage(result.Message)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face recognition service is unavailable.");
        }

        return new FaceRecognitionResult
        {
            Success = false,
            Message = "تعذر التعرف على الوجه حالياً. حاول مرة أخرى بصورة أوضح."
        };
    }

    private static string GetContentType(string? fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
    }

    private static FaceRecognitionResponse? DeserializeRecognitionResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FaceRecognitionResponse>(responseBody, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static FaceServiceMessageResponse? DeserializeMessageResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FaceServiceMessageResponse>(responseBody, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractServiceMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }

            if (root.TryGetProperty("detail", out var detailElement))
            {
                if (detailElement.ValueKind == JsonValueKind.String)
                {
                    return detailElement.GetString();
                }

                if (detailElement.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var item in detailElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("msg", out var msgElement) &&
                            msgElement.ValueKind == JsonValueKind.String)
                        {
                            var message = msgElement.GetString();
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                parts.Add(message);
                            }
                        }
                    }

                    if (parts.Count > 0)
                    {
                        return string.Join(" ", parts);
                    }
                }
            }
        }
        catch
        {
            return responseBody;
        }

        return null;
    }

    private static string? TranslateFaceServiceMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.Trim();
        var lower = normalized.ToLowerInvariant();

        if (lower.Contains("no face detected"))
        {
            return "لم يتم اكتشاف وجه واضح في الصورة. قرّب الوجه من الكاميرا، واجعل الإضاءة أمامك، ثم حاول مرة أخرى.";
        }

        if (lower.Contains("invalid image file"))
        {
            return "ملف الصورة غير صالح. أعد التقاط الصورة ثم حاول مرة أخرى.";
        }

        if (lower.Contains("system is empty"))
        {
            return "لم يتم العثور على أي بصمات وجه مسجلة في النظام حتى الآن.";
        }

        if (lower.Contains("service is unavailable") || lower.Contains("connection") || lower.Contains("timed out"))
        {
            return "خدمة التعرف على الوجه غير متاحة حالياً. حاول مرة أخرى بعد قليل.";
        }

        return normalized;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public class FaceRecognitionResponse
{
    public string? Message { get; set; }
    public List<RecognizedFace> Recognized { get; set; } = new();
}

public class FaceServiceMessageResponse
{
    public string? Message { get; set; }
}

public class RecognizedFace
{
    public int StudentId { get; set; }
    public double Confidence { get; set; }
}
