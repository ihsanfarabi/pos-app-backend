using System.Net.Http.Headers;

namespace PosApp.Integration.Tests;

public sealed class AuthAndMenuFlowTests : IClassFixture<PosApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthAndMenuFlowTests(PosApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginAndFetchMenu_ReturnsSeededItems()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@example.com", "ChangeMe123!"));

        loginResponse.EnsureSuccessStatusCode();

        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(tokens);
        Assert.Equal("Bearer", tokens!.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));

        using var menuRequest = new HttpRequestMessage(HttpMethod.Get, "/api/menu");
        menuRequest.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);

        var menuResponse = await _client.SendAsync(menuRequest);

        menuResponse.EnsureSuccessStatusCode();

        var payload = await menuResponse.Content.ReadFromJsonAsync<PaginatedItems<MenuItemResponse>>();

        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Count);
        Assert.NotEmpty(payload.Data);
        Assert.Contains(payload.Data, item => item.Name == "Nasi Goreng Ati Ampela");
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record AuthTokensResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record PaginatedItems<T>(
        [property: JsonPropertyName("pageIndex")] int PageIndex,
        [property: JsonPropertyName("pageSize")] int PageSize,
        [property: JsonPropertyName("count")] long Count,
        [property: JsonPropertyName("data")] IReadOnlyList<T> Data);

    private sealed record MenuItemResponse(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] decimal Price);
}
