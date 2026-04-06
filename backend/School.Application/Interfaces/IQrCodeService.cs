namespace School.Application.Interfaces;

public interface IQrCodeService
{
    string GenerateQrToken(int sessionId);
    bool ValidateQrToken(string token, int sessionId);
}
