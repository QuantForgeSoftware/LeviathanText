using LeviathanText.Contracts;
using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>
/// Per-track/profile aggregation: results are never pooled across material tracks
/// (acceptance-plan.md core principle).
/// </summary>
public class TrackScoringTests
{
    private static TrackLineScore Line(MaterialTrack track, string profile, string reference, string hypothesis) =>
        new(track, profile, RecognitionScorer.Score(reference, hypothesis));

    [Fact]
    public void Groups_by_track_and_profile_with_micro_cer()
    {
        var aggregates = TrackScoring.Aggregate(
        [
            Line(MaterialTrack.Papyrus, "documentary", "αβγδ", "αβγδ"),   // 0/4
            Line(MaterialTrack.Papyrus, "documentary", "αβγδ", "αβγχ"),   // 1/4
            Line(MaterialTrack.Papyrus, "literary", "αβ", "αβ"),
            Line(MaterialTrack.Manuscript, "codex-a", "αβγ", "αβγ"),
        ]);

        Assert.Equal(3, aggregates.Count);
        var documentary = Assert.Single(aggregates, a => a.Track == MaterialTrack.Papyrus && a.Profile == "documentary");
        Assert.Equal(2, documentary.Lines);
        Assert.Equal(8, documentary.ReferenceScalars);
        Assert.Equal(1, documentary.ScalarDistance);
        Assert.Equal(1.0 / 8.0, documentary.Cer, 10);
        Assert.DoesNotContain(aggregates, a => a.Track != MaterialTrack.Papyrus && a.Track != MaterialTrack.Manuscript);
    }

    [Fact]
    public void Track_totals_keep_profiles_as_a_breakdown_not_a_pooled_report()
    {
        var totals = TrackScoring.AggregateWithProfiles(
        [
            Line(MaterialTrack.Papyrus, "documentary", "αβ", "αβ"),
            Line(MaterialTrack.Papyrus, "literary", "γδ", "γχ"),          // 1/2
            Line(MaterialTrack.Inscription, "stone", "αβγ", "αβγ"),
        ]);

        Assert.Equal(2, totals.Count); // one per track; no cross-track object
        var papyri = Assert.Single(totals, a => a.Track == MaterialTrack.Papyrus);
        Assert.Equal(2, papyri.Profiles.Count);
        Assert.Equal(4, papyri.ReferenceScalars);
        Assert.Equal(0.25, papyri.Cer, 10);
        Assert.Single(totals, a => a.Track == MaterialTrack.Inscription);
    }

    [Fact]
    public void Empty_or_unstratified_input_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TrackScoring.Aggregate([]));
        Assert.Throws<ArgumentException>(() => TrackScoring.Aggregate(
            [Line(MaterialTrack.Papyrus, " ", "α", "α")]));
    }
}
