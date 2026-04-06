namespace School.Application.Interfaces;

public interface ILiveSessionService
{
    string GenerateToken(string channelName, string userAccount, uint expireTimeInSeconds);
    string GenerateToken(string channelName, uint uid, uint expireTimeInSeconds);
}
