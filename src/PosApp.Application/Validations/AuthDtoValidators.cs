using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validations;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator(IUserRepository userRepository)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .MustAsync(async (email, cancellation) =>
            {
                var normalized = email.Trim().ToLowerInvariant();
                return !await userRepository.EmailExistsAsync(normalized, cancellation);
            })
            .WithMessage("Email already registered.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.Role)
            .Must(role => string.IsNullOrWhiteSpace(role) || !string.IsNullOrWhiteSpace(role.Trim()))
            .WithMessage("Role must contain non-whitespace characters when provided.");
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
