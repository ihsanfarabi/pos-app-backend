using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Auth.Commands;

public sealed record RegisterUserCommand(RegisterDto Dto) : IRequest<Guid>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(IUserRepository userRepository)
    {
        RuleFor(x => x.Dto.Email)
            .NotEmpty()
            .EmailAddress().WithErrorCode("Email")
            .MaximumLength(320)
            .MustAsync(async (email, cancellation) =>
            {
                var normalized = email.Trim().ToLowerInvariant();
                return !await userRepository.EmailExistsAsync(normalized, cancellation);
            })
            .WithMessage("Email already registered.").WithErrorCode("EmailAlreadyRegistered");

        RuleFor(x => x.Dto.Password)
            .NotEmpty().WithErrorCode("NotEmpty")
            .MinimumLength(8).WithErrorCode("MinLength");

        RuleFor(x => x.Dto.Role)
            .Must(role => string.IsNullOrWhiteSpace(role) || !string.IsNullOrWhiteSpace(role.Trim()))
            .WithMessage("Role must contain non-whitespace characters when provided.")
            .WithErrorCode("RoleInvalid");
    }
}

public class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new DomainException("Email already registered.", "email");
        }

        var role = string.IsNullOrWhiteSpace(dto.Role) ? UserRoles.User : dto.Role.Trim();

        User user;
        try
        {
            user = User.Create(normalizedEmail, role);
        }
        catch (DomainException)
        {
            throw;
        }

        var passwordHash = passwordHasher.HashPassword(user, dto.Password);
        user.SetPasswordHash(passwordHash);

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
