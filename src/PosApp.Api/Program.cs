using PosApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddSerilogLogging()
    .AddPosAppApplicationServices()
    .AddPosAppApiServices()
    .AddPosAppHealthChecks()
    .AddPosAppRateLimiting()
    .AddPosAppSwagger();

var app = builder.Build();

await app.InitialiseDatabaseAsync();

app.UsePosAppForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UsePosAppExceptionHandler();
app.UseTraceIdHeader();

app.UsePosAppSwaggerUI();
app.MapPosAppRootEndpoint();

app.UseRateLimiter();
app.MapPosAppHealthChecks();

app.UseCors();
app.UseAuthentication();
app.UseIdempotencyContext();
app.UseAuthorization();

app.MapPosAppEndpoints();

app.Run();

public partial class Program;
