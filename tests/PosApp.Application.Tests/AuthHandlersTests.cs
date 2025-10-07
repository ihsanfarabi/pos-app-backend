using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Auth.Commands;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Tests;

public class AuthHandlersTests
{
    [Fact]
    public async Task RegisterUserCommandHandler_PersistsHashedUser()
    {
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.HashPassword(Arg.Any<User>(), "pass123").Returns("hashed");
        User? captured = null;
        userRepository.AddAsync(Arg.Do<User>(user => captured = user), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new RegisterUserCommandHandler(userRepository, passwordHasher);
        var command = new RegisterUserCommand(new RegisterDto("Test@Example.com", "pass123", "Admin"));

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(captured);
        Assert.Equal(id, captured!.Id);
        Assert.Equal("test@example.com", captured.Email);
        Assert.Equal("admin", captured.Role);
        Assert.Equal("hashed", captured.PasswordHash);
        await userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        passwordHasher.Received(1).HashPassword(captured, "pass123");
    }

    [Fact]
    public async Task RegisterUserCommandHandler_WhenEmailExists_ThrowsDomainException()
    {
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var handler = new RegisterUserCommandHandler(userRepository, passwordHasher);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new RegisterUserCommand(new RegisterDto("user@example.com", "pass", null)), CancellationToken.None));

        Assert.Equal("Email already registered.", exception.Message);
        Assert.Equal("email", exception.PropertyName);
    }

    [Fact]
    public async Task LoginCommandHandler_WhenUserNotFound_ReturnsNull()
    {
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var tokenService = Substitute.For<ITokenService>();
        var handler = new LoginCommandHandler(userRepository, passwordHasher, tokenService);

        var result = await handler.Handle(new LoginCommand(new LoginDto("user@example.com", "pass")), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginCommandHandler_WhenPasswordInvalid_ReturnsNull()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var user = User.Create("user@example.com", "user");
        user.SetPasswordHash("stored");
        userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "pass")
            .Returns(PasswordVerificationResult.Failed);
        var tokenService = Substitute.For<ITokenService>();
        var handler = new LoginCommandHandler(userRepository, passwordHasher, tokenService);

        var result = await handler.Handle(new LoginCommand(new LoginDto("user@example.com", "pass")), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginCommandHandler_WhenSuccess_ReturnsTokens()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var user = User.Create("user@example.com", "user");
        user.SetPasswordHash("stored");
        userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "pass")
            .Returns(PasswordVerificationResult.Success);
        var tokenService = Substitute.For<ITokenService>();
        var accessToken = new AccessToken("access", 60);
        var refreshToken = new RefreshToken("refresh", DateTimeOffset.UtcNow.AddDays(1));
        tokenService.CreateAccessToken(user).Returns(accessToken);
        tokenService.CreateRefreshToken(user).Returns(refreshToken);
        var handler = new LoginCommandHandler(userRepository, passwordHasher, tokenService);

        var result = await handler.Handle(new LoginCommand(new LoginDto("user@example.com", "pass")), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(accessToken, result!.AccessToken);
        Assert.Equal(refreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_WhenTokenMissing_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        var userRepository = Substitute.For<IUserRepository>();
        var handler = new RefreshTokenCommandHandler(tokenService, userRepository);

        var result = await handler.Handle(new RefreshTokenCommand(" "), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_WhenValidationFails_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateRefreshToken("token").Returns((RefreshTokenValidationResult?)null);
        var userRepository = Substitute.For<IUserRepository>();
        var handler = new RefreshTokenCommandHandler(tokenService, userRepository);

        var result = await handler.Handle(new RefreshTokenCommand("token"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_WhenUserMissing_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        var validation = new RefreshTokenValidationResult(Guid.NewGuid());
        tokenService.ValidateRefreshToken("token").Returns(validation);
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.GetByIdAsync(validation.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new RefreshTokenCommandHandler(tokenService, userRepository);

        var result = await handler.Handle(new RefreshTokenCommand("token"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenCommandHandler_WhenSuccess_ReturnsTokens()
    {
        var tokenService = Substitute.For<ITokenService>();
        var validation = new RefreshTokenValidationResult(Guid.NewGuid());
        tokenService.ValidateRefreshToken("token").Returns(validation);
        var userRepository = Substitute.For<IUserRepository>();
        var user = User.Create("user@example.com", "user");
        user.SetPasswordHash("hash");
        userRepository.GetByIdAsync(validation.UserId, Arg.Any<CancellationToken>()).Returns(user);
        var accessToken = new AccessToken("access", 120);
        var refreshToken = new RefreshToken("refresh", DateTimeOffset.UtcNow.AddDays(1));
        tokenService.CreateAccessToken(user).Returns(accessToken);
        tokenService.CreateRefreshToken(user).Returns(refreshToken);
        var handler = new RefreshTokenCommandHandler(tokenService, userRepository);

        var result = await handler.Handle(new RefreshTokenCommand("token"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(accessToken, result!.AccessToken);
        Assert.Equal(refreshToken, result.RefreshToken);
    }
}
