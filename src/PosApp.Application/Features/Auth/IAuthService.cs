using PosApp.Application.Contracts;

namespace PosApp.Application.Features.Auth;

public interface IAuthService
{
    Task<Guid> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken);

    Task<LoginResult?> LoginAsync(LoginDto dto, CancellationToken cancellationToken);

    Task<LoginResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
