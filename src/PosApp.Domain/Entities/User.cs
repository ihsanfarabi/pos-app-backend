using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Entities;

public class User
{
    private User()
    {
    }

    private User(Guid id, string email, string role)
    {
        Id = id;
        Email = email;
        Role = role;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = default!;

    public string PasswordHash { get; private set; } = default!;

    public string Role { get; private set; } = UserRoles.User;

    public static User Create(string email, string role)
    {
        var normalizedEmail = NormalizeEmail(email);
        EnsureValidEmail(normalizedEmail);

        var normalizedRole = NormalizeRole(role);
        return new User(Guid.NewGuid(), normalizedEmail, normalizedRole);
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.", "password");
        }

        PasswordHash = passwordHash;
    }

    public void SetRole(string role)
    {
        var normalizedRole = NormalizeRole(role);
        Role = normalizedRole;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static void EnsureValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.", "email");
        }

        if (!email.Contains('@'))
        {
            throw new DomainException("Email is invalid.", "email");
        }
    }

    private static string NormalizeRole(string role)
    {
        return string.IsNullOrWhiteSpace(role) ? UserRoles.User : role.Trim().ToLowerInvariant();
    }
}

public static class UserRoles
{
    public const string User = "user";
    public const string Admin = "admin";
}
