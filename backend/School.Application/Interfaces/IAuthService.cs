namespace School.Application.Interfaces;

public interface IAuthService
{
    Task<string> LoginAsync(string email, string password);
    Task<bool> RegisterAdminAsync(string userName, string email, string password);
}
