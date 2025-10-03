using PosApp.Application.Abstractions.Security;

namespace PosApp.Application.Features.Auth;

public sealed record LoginResult(AccessToken AccessToken, RefreshToken RefreshToken);

public sealed record UserProfile(Guid Id, string Email, string Role);
