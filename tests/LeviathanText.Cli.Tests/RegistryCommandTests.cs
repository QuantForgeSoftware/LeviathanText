namespace LeviathanText.Cli.Tests;

public sealed class RegistryCommandTests
{
    [Theory]
    [InlineData(false, "pass --allow-host")]
    [InlineData(true, "not in the allow-list")]
    public void Pull_validates_index_trust_before_attempting_transport(bool allowDifferentHost, string error)
    {
        var root = Path.Combine(Path.GetTempPath(), $"lt-registry-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            // This address must never be contacted: the trust error should precede a
            // connection error, whether the caller omitted hosts or allowed another host.
            string[] flags = allowDifferentHost ? ["--allow-host", "unapproved.example", "--insecure-http"] : [];
            var result = LeviathanText.Cli.Program.Main([
                "--store", Path.Combine(root, "store.db"), "models", "pull",
                "http://127.0.0.1:1/index", "synthetic", Path.Combine(root, "models"), .. flags,
            ]);

            Assert.Equal(1, result);
            Assert.Contains(error, captured.ToString());
            Assert.False(Directory.Exists(Path.Combine(root, "models")));
        }
        finally
        {
            Console.SetError(original);
            Directory.Delete(root, recursive: true);
        }
    }
}
