using PosApp.Domain.Entities;

namespace PosApp.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);

    PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1
}
