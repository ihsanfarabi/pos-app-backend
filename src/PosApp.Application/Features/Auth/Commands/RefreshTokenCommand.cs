using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;

namespace PosApp.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult?>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}

public class RefreshTokenCommandHandler(
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
