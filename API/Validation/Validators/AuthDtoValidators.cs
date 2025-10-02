using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PosApi.Contracts;
using PosApi.Infrastructure;

namespace PosApi.Validation.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator(AppDbContext dbContext)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .MustAsync(async (email, cancellation) =>
            {
                var normalized = email.Trim().ToLowerInvariant();
                return !await dbContext.Users.AnyAsync(u => u.Email == normalized, cancellation);
            })
            .WithMessage("Email already registered");

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
