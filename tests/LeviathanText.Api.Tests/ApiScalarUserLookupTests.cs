using System.Net;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LeviathanText.Api.Tests;

public sealed class ApiScalarUserLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_local_mode_is_available_only_when_no_accounts_exist(bool disabledOnly)
    {
        var directory = Directory.CreateTempSubdirectory("lt-api-user-presence-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            const string key = "fixture-disabled-account-key";
            if (disabledOnly)
            {
                using var store = new LocalStore(database);
                var account = store.CreateUser("disabled", UserRole.Admin, UserKeys.Hash(key));
                store.DisableUser(account.Id);
            }
            using var factory = new NoBootstrapFactory(database);
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/v1/projects");
            Assert.Equal(disabledOnly ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, response.StatusCode);
            if (disabledOnly)
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", key);
                using var disabled = await client.GetAsync("/api/v1/projects");
                Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class NoBootstrapFactory(string database) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", database);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["LeviathanText:Auth:ApiKey"] = "" }));
        }
    }
}
