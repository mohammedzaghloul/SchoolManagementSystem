namespace School.Application.Interfaces;

public interface IFaceRecognitionService
{
    Task<FaceTrainingResult> TrainFaceAsync(int studentId, byte[] imageBytes, string fileName);
    Task<FaceRecognitionResult> RecognizeFaceAsync(byte[] imageBytes, string fileName);
}

public class FaceTrainingResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class FaceRecognitionResult
{
    public bool Success { get; set; }
    public int StudentId { get; set; }
    public double Confidence { get; set; }
    public string? Message { get; set; }
}
