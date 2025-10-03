using Microsoft.AspNetCore.Identity;
using PosApp.Application.Abstractions.Security;
using PosApp.Domain.Entities;
using IdentityPasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;
using ApplicationPasswordVerificationResult = PosApp.Application.Abstractions.Security.PasswordVerificationResult;

namespace PosApp.Infrastructure.Security;

public sealed class PasswordHasherAdapter(IPasswordHasher<User> innerHasher) : IPasswordHasher
{
    public string HashPassword(User user, string password)
    {
        return innerHasher.HashPassword(user, password);
    }

    public ApplicationPasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        var result = innerHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
        return result switch
        {
            IdentityPasswordVerificationResult.Failed => ApplicationPasswordVerificationResult.Failed,
            _ => ApplicationPasswordVerificationResult.Success
        };
    }
}
