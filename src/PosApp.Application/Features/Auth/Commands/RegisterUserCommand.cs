using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Auth.Commands;

public sealed record RegisterUserCommand(RegisterDto Dto) : IRequest<Guid>;

internal sealed class RegisterUserCommandHandler(
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
            throw new ValidationException("Email already registered.", "email");
        }

        var role = string.IsNullOrWhiteSpace(dto.Role) ? UserRoles.User : dto.Role.Trim();

        User user;
        try
        {
            user = User.Create(normalizedEmail, role);
        }
        catch (DomainException ex)
        {
            throw new ValidationException(ex.Message, ex.PropertyName);
        }

        var passwordHash = passwordHasher.HashPassword(user, dto.Password);
        user.SetPasswordHash(passwordHash);

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
