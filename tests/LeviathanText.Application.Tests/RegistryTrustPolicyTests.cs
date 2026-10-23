using LeviathanText.Application;

namespace LeviathanText.Application.Tests;

public class RegistryTrustPolicyTests
{
    private static RemoteModelEntry Entry(string url, string? sha = null) => new(
        "m", "Model", "recognition", new Uri(url),
        sha ?? new string('a', 64));

    [Fact]
    public void Default_denies_every_host()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RegistryTrustPolicy.DenyAll.Validate(Entry("https://example.test/model.zip")));
    }

    [Fact]
    public void Explicit_https_host_is_allowed_and_non_https_is_rejected()
    {
        var policy = RegistryTrustPolicy.Allow("example.test");
        policy.Validate(Entry("https://example.test/model.zip"));
        Assert.Throws<InvalidDataException>(() =>
            policy.Validate(Entry("http://example.test/model.zip")));
        Assert.Throws<InvalidDataException>(() =>
            policy.Validate(Entry("https://other.test/model.zip")));
    }

    [Fact]
    public void Wildcard_host_and_checksum_rules_are_enforced()
    {
        var policy = RegistryTrustPolicy.Allow("*.example.test");
        policy.Validate(Entry("https://cdn.example.test/model.zip"));
        Assert.Throws<InvalidDataException>(() =>
            policy.Validate(Entry("https://example.test/model.zip", new string('a', 64)) with { Sha256 = null }));
    }
}
