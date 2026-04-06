using MediatR;
using School.Application.Interfaces;

namespace School.Application.Features.Account.Commands;

public class RegisterAdminCommand : IRequest<bool>
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}

public class RegisterAdminCommandHandler : IRequestHandler<RegisterAdminCommand, bool>
{
    private readonly IAuthService _authService;

    public RegisterAdminCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<bool> Handle(RegisterAdminCommand request, CancellationToken cancellationToken)
    {
        return await _authService.RegisterAdminAsync(request.UserName, request.Email, request.Password);
    }
}
