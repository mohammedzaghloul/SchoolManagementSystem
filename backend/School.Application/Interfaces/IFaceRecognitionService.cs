namespace School.Application.Interfaces;

public interface IFaceRecognitionService
{
    Task<bool> TrainFaceAsync(int studentId, byte[] imageBytes, string fileName);
    Task<FaceRecognitionResult> RecognizeFaceAsync(byte[] imageBytes, string fileName);
}

public class FaceRecognitionResult
{
    public bool Success { get; set; }
    public int StudentId { get; set; }
    public double Confidence { get; set; }
}
