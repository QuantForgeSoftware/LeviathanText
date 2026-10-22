using LeviathanText.Formats;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

public class SegmentationDatasetTests
{
    private static readonly (string, int)[] Aux = [("_start_separator", 0), ("_end_separator", 1)];

    [Fact]
    public void Class_map_counts_classes_and_keeps_canonical_order()
    {
        var map = SegmentationClassMap.Create(
            Aux,
            [("$pag", 2), ("$pac", 3), ("$tip", 4), ("$par", 5)],
            [("$pag", 6), ("$pac", 7), ("$tip", 8), ("$par", 9)]);

        Assert.Equal(10, map.NumClasses);
        Assert.Equal(5, map.Canonical["baselines"]["$par"]);
        Assert.Empty(map.MergedClasses["baselines"]);
    }

    [Fact]
    public void Filtering_a_mapping_removes_classes_not_in_the_input()
    {
        var map = SegmentationClassMap.Create(
            Aux,
            [("$tip", 2), ("$par", 3)],
            [("$tip", 4), ("$pac", 5)]);
        var target = SegmentationTargetBuilder.Build(10, 10, map,
            regions: [new SegmentationRegion("$pag", Rectangle(1, 1, 5, 5))]);

        Assert.Equal(6, map.NumClasses);
        Assert.False(map.TryGetIndex("baselines", "$pag", out _));
        Assert.False(map.TryGetIndex("regions", "$par", out _));
        Assert.Equal(0, target.Cast<float>().Sum()); // unknown region label is filtered
    }

    [Fact]
    public void Merging_classes_uses_first_label_as_canonical_and_lists_aliases()
    {
        var map = SegmentationClassMap.Create(
            Aux,
            [("$pag", 2), ("$pac", 3), ("$par", 4), ("$tip", 4)],
            [("$pag", 5), ("$pac", 6), ("$tip", 7), ("$par", 7)]);

        Assert.Equal(8, map.NumClasses);
        Assert.Equal(4, map.Canonical["baselines"]["$par"]);
        Assert.DoesNotContain("$tip", map.Canonical["baselines"].Keys);
        Assert.Equal(["$tip"], map.MergedClasses["baselines"]["$par"]);
        Assert.Equal(["$par"], map.MergedClasses["regions"]["$tip"]);
    }

    [Fact]
    public void Negative_class_index_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SegmentationClassMap.Create(
            Aux, [("bad", -1)], []));
    }

    [Fact]
    public void Target_rasterizes_regions_and_baselines_without_choosing_a_winner()
    {
        var map = SegmentationClassMap.Create(Aux, [("line", 2)], [("region", 3)]);
        var target = SegmentationTargetBuilder.Build(30, 20, map,
            regions: [new SegmentationRegion("region", Rectangle(5, 5, 15, 15))],
            baselines: [new SegmentationBaseline("line", [new PointD(2, 18), new PointD(25, 18)])]);

        Assert.Equal(4, target.GetLength(0));
        Assert.Equal(20, target.GetLength(1));
        Assert.Equal(30, target.GetLength(2));
        Assert.Equal(1, target[3, 10, 10]); // region
        Assert.Equal(1, target[2, 18, 10]); // baseline
        Assert.Equal(0, target[0, 0, 0]); // aux
        Assert.Equal(0, target[3, 18, 10]); // channels remain independent
    }

    private static PointD[] Rectangle(int left, int top, int right, int bottom) =>
        [new PointD(left, top), new PointD(right, top), new PointD(right, bottom), new PointD(left, bottom)];
}
