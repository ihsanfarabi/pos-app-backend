using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PosApp.Integration.Tests;

public sealed class PosApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"posapp-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath};Mode=ReadWriteCreate;Cache=Shared",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-please-change",
                ["RefreshJwt:Issuer"] = "pos-app",
                ["RefreshJwt:Audience"] = "pos-app",
                ["RefreshJwt:SigningKey"] = "integration-tests-refresh-key-change",
                ["RefreshJwt:TtlDays"] = "7"
            };

            configurationBuilder.AddInMemoryCollection(settings);
        });

        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath};Mode=ReadWriteCreate;Cache=Shared");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-please-change");
        builder.UseSetting("RefreshJwt:Issuer", "pos-app");
        builder.UseSetting("RefreshJwt:Audience", "pos-app");
        builder.UseSetting("RefreshJwt:SigningKey", "integration-tests-refresh-key-change");
        builder.UseSetting("RefreshJwt:TtlDays", "7");

        builder.ConfigureServices(_ =>
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
