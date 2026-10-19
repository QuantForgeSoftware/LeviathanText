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

    [Fact]
    public void Resource_and_transport_defaults_are_explicit()
    {
        var settings = Valid();

        Assert.Equal(TimeSpan.FromSeconds(60), settings.Timeout);
        Assert.Equal(512, settings.MaxOutputTokens);
        Assert.Equal(1_048_576, settings.MaxResponseBytes);
        Assert.Equal(20_000, settings.MaxPromptCharacters);
        Assert.Equal(3, settings.MaxAttempts);
        Assert.Equal(0, settings.MaxRequests);
        Assert.Null(settings.MaxSpendUsd);
        Assert.Null(settings.OllamaThink);
        Assert.False(settings.UseMaxCompletionTokens);
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
        yield return [Valid() with { Protocol = AiWireProtocol.OpenAiChatCompletions, OllamaThink = false }];
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Invalid_settings_are_rejected(AiProviderSettings settings)
    {
        var ex = Assert.Throws<AiProviderException>(settings.Validate);
        Assert.Equal(AiFailureReasons.ConfigurationInvalid, ex.Reason);
    }
}
