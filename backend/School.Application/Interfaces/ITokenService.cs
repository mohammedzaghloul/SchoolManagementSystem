namespace School.Application.Interfaces;

public interface ITokenService
{
    string CreateToken(string userId, string email, string role, string fullName);
}
