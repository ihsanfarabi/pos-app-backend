using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;

namespace PosApp.Application.Features.Auth.Commands;

public sealed record LoginCommand(LoginDto Dto) : IRequest<LoginResult?>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Dto.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Dto.Password)
            .NotEmpty();
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
    : IRequestHandler<LoginCommand, LoginResult?>
{
    public async Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user);
        return new LoginResult(accessToken, refreshToken);
    }
}
