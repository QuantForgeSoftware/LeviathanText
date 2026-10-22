using System.Text.Json;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class OffsetFixtureTests
{
    [Fact]
    public void Shared_browser_and_csharp_offset_fixture_matches_scalar_contract()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "offset-fixtures.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var testCase in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var text = testCase.GetProperty("text").GetString()!;
            var utf16 = testCase.GetProperty("utf16Offsets").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var scalars = testCase.GetProperty("scalarOffsets").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var name = testCase.GetProperty("name").GetString()!;

            Assert.Equal(scalars[^1], ScalarText.ScalarLength(text));
            for (int i = 0; i < utf16.Length; i++)
            {
                Assert.Equal(scalars[i], ScalarText.Utf16IndexToScalar(text, utf16[i]));
                Assert.Equal(utf16[i], ScalarText.ScalarToUtf16Index(text, scalars[i]));
            }
            Assert.True(utf16.Length > 0, name);
        }
    }
}
