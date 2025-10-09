using PosApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddSerilogLogging()
    .AddPosAppApplicationServices()
    .AddPosAppApiServices()
    .AddPosAppSwagger();

var app = builder.Build();

await app.InitialiseDatabaseAsync();

app.UsePosAppForwardedHeaders();
app.UseExceptionHandler();
app.UseTraceIdHeader();

app.UsePosAppSwaggerUI();
app.MapPosAppRootEndpoint();

app.UseCors();
app.UseAuthentication();
app.UseIdempotencyContext();
app.UseAuthorization();

app.MapPosAppEndpoints();

app.Run();

public partial class Program;
