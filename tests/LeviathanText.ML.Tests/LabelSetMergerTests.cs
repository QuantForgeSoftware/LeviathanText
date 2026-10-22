using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

public class LabelSetMergerTests
{
    [Fact]
    public void New_mode_drops_labels_only_in_the_old_set()
    {
        var existing = new LabelSet(["x", "ẽ"]);
        var resized = LabelSetMerger.Resize(existing, ["9"], LabelSetMergeMode.New);

        Assert.Equal(["9"], resized.Labels);
        Assert.Equal([1], resized.EncodeText("9"));
        Assert.Throws<LeviathanException>(() => resized.EncodeText("x"));
    }

    [Fact]
    public void Union_mode_keeps_old_and_appends_new_labels()
    {
        var existing = new LabelSet(["x", "ẽ"]);
        var resized = LabelSetMerger.Resize(existing, ["9"], LabelSetMergeMode.Union);

        Assert.Equal(["x", "ẽ", "9"], resized.Labels);
        Assert.NotEmpty(resized.EncodeText("x9"));
    }

    [Fact]
    public void Fail_mode_refuses_an_incompatible_vocabulary()
    {
        var existing = new LabelSet(["x"]);
        var ex = Assert.Throws<LeviathanException>(() =>
            LabelSetMerger.Resize(existing, ["9"], LabelSetMergeMode.Fail));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);

        var compatible = LabelSetMerger.Resize(existing, ["x"], LabelSetMergeMode.Fail);
        Assert.Same(existing, compatible);
    }

    [Fact]
    public void Nfd_normalization_is_explicit_and_affects_matching()
    {
        var existing = new LabelSet(["x"]);
        var nfd = "ẽ".Normalize(NormalizationForm.FormD);
        var resized = LabelSetMerger.Resize(existing, ["ẽ"], LabelSetMergeMode.Union, normalizeNfd: true);

        Assert.Contains(nfd, resized.Labels);
        Assert.NotEmpty(resized.EncodeText(nfd));
        Assert.Throws<LeviathanException>(() => resized.EncodeText("ẽ"));
    }
}
