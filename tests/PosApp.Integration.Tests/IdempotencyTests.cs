using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PosApp.Integration.Tests;

public sealed class IdempotencyTests : IClassFixture<PosApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IdempotencyTests(PosApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTicket_WithSameKey_ReplaysResponse()
    {
        var tokens = await AuthenticateAsync();
        var idempotencyKey = Guid.NewGuid().ToString();

        var firstResponse = await PostCreateTicketAsync(tokens, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<TicketCreatedResponse>();
        Assert.NotNull(firstPayload);

        var secondResponse = await PostCreateTicketAsync(tokens, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<TicketCreatedResponse>();
        Assert.NotNull(secondPayload);

        Assert.Equal(firstPayload!.Id, secondPayload!.Id);
    }

    [Fact]
    public async Task AddTicketLine_WithDifferentPayload_SameKey_ReturnsProblem()
    {
        var tokens = await AuthenticateAsync();
        var ticketKey = Guid.NewGuid().ToString();
        var menuItems = await GetMenuItemsAsync(tokens);
        var menuItemId = menuItems.Data.First().Id;

        var ticketResponse = await PostCreateTicketAsync(tokens, ticketKey);
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<TicketCreatedResponse>();
        Assert.NotNull(ticket);

        var idempotencyKey = Guid.NewGuid().ToString();

        var firstLineResponse = await PostAddTicketLineAsync(tokens, ticket!.Id, idempotencyKey, new AddLineRequest(menuItemId, 1));
        Assert.Equal(HttpStatusCode.Created, firstLineResponse.StatusCode);

        var conflictResponse = await PostAddTicketLineAsync(tokens, ticket.Id, idempotencyKey, new AddLineRequest(menuItemId, 2));
        Assert.Equal(HttpStatusCode.BadRequest, conflictResponse.StatusCode);

        var json = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("detail", out var detail));
        Assert.Equal("Idempotency key conflict: payload does not match previous request.", detail.GetString());
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

    private async Task<HttpResponseMessage> PostCreateTicketAsync(AuthTokensResponse tokens, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        request.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAddTicketLineAsync(AuthTokensResponse tokens, Guid ticketId, string key, AddLineRequest payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/lines");
        request.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(payload);
        return await _client.SendAsync(request);
    }

    private async Task<PaginatedItems<MenuItemResponse>> GetMenuItemsAsync(AuthTokensResponse tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/menu");
        request.Headers.Authorization = new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PaginatedItems<MenuItemResponse>>();
        return payload ?? throw new InvalidOperationException("Menu response was empty.");
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
