using LeviathanText.AI;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>Invalid provider configuration fails as configuration-invalid, not at request time.</summary>
public class ProviderSettingsTests
{
    private static AiProviderSettings Valid() => new()
    {
        ProviderId = "local/test",
        Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OllamaChat,
        Endpoint = new Uri("http://127.0.0.1:11434"),
        ModelId = "greek-local",
    };

    [Fact]
    public void Valid_settings_pass()
    {
        Valid().Validate();
    }

    public static IEnumerable<object[]> InvalidSettings()
    {
        yield return [Valid() with { ProviderId = "" }];
        yield return [Valid() with { ModelId = " " }];
        yield return [Valid() with { Endpoint = new Uri("ftp://127.0.0.1/") }];
        yield return [Valid() with { Timeout = TimeSpan.Zero }];
        yield return [Valid() with { MaxOutputTokens = 0 }];
        yield return [Valid() with { MaxResponseBytes = 100 }];
        yield return [Valid() with { MaxPromptCharacters = 100 }];
        yield return [Valid() with { MaxAttempts = 6 }];
        yield return [Valid() with { MaxRequests = -1 }];
        yield return [Valid() with { MaxSpendUsd = 1m }]; // price missing
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Invalid_settings_are_rejected(AiProviderSettings settings)
    {
        var ex = Assert.Throws<AiProviderException>(settings.Validate);
        Assert.Equal(AiFailureReasons.ConfigurationInvalid, ex.Reason);
    }
}
