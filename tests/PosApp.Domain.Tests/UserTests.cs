using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Create_WithValidInputs_NormalizesValues()
    {
        var user = User.Create("  JOHN@example.com ", " Admin ");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("john@example.com", user.Email);
        Assert.Equal("admin", user.Role);
        Assert.Null(user.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_WithMissingEmail_ThrowsDomainException(string invalidEmail)
    {
        var exception = Assert.Throws<DomainException>(() => User.Create(invalidEmail, "user"));

        Assert.Equal("Email is required.", exception.Message);
        Assert.Equal("email", exception.PropertyName);
    }

    [Fact]
    public void Create_WithInvalidEmail_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => User.Create("invalid-email", "user"));

        Assert.Equal("Email is invalid.", exception.Message);
        Assert.Equal("email", exception.PropertyName);
    }

    [Fact]
    public void Create_WithMissingRole_DefaultsToUserRole()
    {
        var user = User.Create("user@example.com", "");

        Assert.Equal(UserRoles.User, user.Role);
    }

    [Fact]
    public void SetPasswordHash_WithValidHash_SetsValue()
    {
        var user = User.Create("user@example.com", "user");

        user.SetPasswordHash("hash");

        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_WithMissingHash_ThrowsDomainException()
    {
        var user = User.Create("user@example.com", "user");

        var exception = Assert.Throws<DomainException>(() => user.SetPasswordHash(" "));

        Assert.Equal("Password hash is required.", exception.Message);
        Assert.Equal("password", exception.PropertyName);
    }

    [Fact]
    public void SetRole_NormalizesRole()
    {
        var user = User.Create("user@example.com", "user");

        user.SetRole(" Manager ");

        Assert.Equal("manager", user.Role);
    }
}
