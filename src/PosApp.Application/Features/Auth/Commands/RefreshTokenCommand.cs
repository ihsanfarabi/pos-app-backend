using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Features.Auth;

namespace PosApp.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult?>;

internal sealed class RefreshTokenCommandHandler(
    ITokenService tokenService,
    IUserRepository userRepository)
    : IRequestHandler<RefreshTokenCommand, LoginResult?>
{
    public async Task<LoginResult?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var validationResult = tokenService.ValidateRefreshToken(request.RefreshToken);
        if (validationResult is null)
        {
            return null;
        }

        var user = await userRepository.GetByIdAsync(validationResult.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user);
        return new LoginResult(accessToken, refreshToken);
    }
}
