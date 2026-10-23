using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class GreekDiplomaticProfileTests
{
    [Fact]
    public void Profile_and_sign_inventory_are_versioned_and_unique()
    {
        Assert.Equal("greek-diplomatic-1.0-draft", GreekDiplomaticProfile.Id);
        Assert.Equal("1.0-draft", GreekDiplomaticProfile.Version);
        Assert.Equal(GreekDiplomaticProfile.Id, GreekTextProfile.ProfileId);
        Assert.Equal(GreekDiplomaticProfile.Version, GreekTextProfile.Version);
        Assert.Equal(GreekDiplomaticProfile.Signs.Count,
            GreekDiplomaticProfile.Signs.Select(sign => sign.Id).Distinct().Count());
        Assert.Contains(GreekDiplomaticProfile.Signs, sign => sign.Id == "overline" && sign.CodePoint == 0x0305);
    }

    [Fact]
    public void Decisions_are_explicit_for_l1_spacing_underdots_and_correction_order()
    {
        Assert.False(GreekDiplomaticProfile.PreservesL1Spaces);
        Assert.Equal(GreekDiplomaticProfile.UnderdotPolicy.PerCharacterPrimary, GreekDiplomaticProfile.Underdots);
        Assert.Contains(GreekDiplomaticProfile.Rules, rule => rule.Id == "correction-order" && rule.Decision.Contains("only with evidence"));
        Assert.Contains(GreekDiplomaticProfile.Rules, rule => rule.Id == "scriptio-continua");
        Assert.True(GreekDiplomaticProfile.IsKnownSign(0x0305));
        Assert.False(GreekDiplomaticProfile.IsKnownSign(0x1F600));
    }
}
