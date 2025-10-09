using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PosApp.Integration.Tests;

public sealed class ExceptionHandlingTests : IClassFixture<PosApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExceptionHandlingTests(PosApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddTicketLine_WhenMenuItemMissing_ReturnsDomainProblem()
    {
        var tokens = await AuthenticateAsync();
        var ticketId = await CreateTicketAsync(tokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/lines");
        request.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new AddLineRequest(Guid.NewGuid(), 1));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Undefined, problem.ValueKind);

        Assert.Equal("Bad Request", problem.GetProperty("title").GetString());
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.GetProperty("status").GetInt32());
        Assert.Equal("Menu item not found.", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    private async Task<Guid> CreateTicketAsync(AuthTokensResponse tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        request.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TicketCreatedResponse>();
        return payload?.Id ?? throw new InvalidOperationException("Ticket creation response was empty.");
    }

    private async Task<AuthTokensResponse> AuthenticateAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@example.com", "ChangeMe123!"));

        loginResponse.EnsureSuccessStatusCode();

        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        return tokens ?? throw new InvalidOperationException("Authentication response was empty.");
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record AuthTokensResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record TicketCreatedResponse([property: JsonPropertyName("id")] Guid Id);

    private sealed record AddLineRequest(
        [property: JsonPropertyName("menuItemId")] Guid MenuItemId,
        [property: JsonPropertyName("qty")] int Qty);
}
