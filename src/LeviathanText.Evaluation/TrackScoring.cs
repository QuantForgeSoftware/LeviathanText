using LeviathanText.Contracts;

namespace LeviathanText.Evaluation;

/// <summary>One line's recognition score with its material track and profile stratum.</summary>
public sealed record TrackLineScore(MaterialTrack Track, string Profile, RecognitionScore Score);

/// <summary>
/// Per-track/profile totals. There is deliberately no API that pools tracks
/// (acceptance-plan.md core principle): a caller can only obtain one aggregate per
/// track/profile and must report them separately.
/// </summary>
public sealed record TrackAggregate(
    MaterialTrack Track,
    string Profile,
    int Lines,
    int ReferenceScalars,
    int ScalarDistance,
    double Cer)
{
    /// <summary>Per-profile aggregates for this track; never merged with another track.</summary>
    public IReadOnlyList<TrackAggregate> Profiles { get; init; } = [];
}

/// <summary>Groups line scores by track and profile, summing distances micro-style.</summary>
public static class TrackScoring
{
    public static IReadOnlyList<TrackAggregate> Aggregate(IEnumerable<TrackLineScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);

        var lines = scores.ToArray();
        if (lines.Length == 0) throw new ArgumentException("At least one score is required.", nameof(scores));
        if (lines.Any(line => string.IsNullOrWhiteSpace(line.Profile)))
            throw new ArgumentException("Every score needs a profile stratum (for example hand or capture).", nameof(scores));

        return lines
            .GroupBy(line => (line.Track, line.Profile))
            .Select(group => Build(group.Key.Track, group.Key.Profile, group))
            .OrderBy(aggregate => aggregate.Track)
            .ThenBy(aggregate => aggregate.Profile, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Track totals together with their per-profile breakdown. The returned objects are
    /// still separate per track; there is no combined object.
    /// </summary>
    public static IReadOnlyList<TrackAggregate> AggregateWithProfiles(IEnumerable<TrackLineScore> scores)
    {
        var perProfile = Aggregate(scores);
        var byTrack = perProfile.GroupBy(aggregate => aggregate.Track).ToArray();

        return byTrack
            .Select(group => BuildFromProfiles(group.Key, group.ToArray()))
            .OrderBy(aggregate => aggregate.Track)
            .ToArray();
    }

    private static TrackAggregate BuildFromProfiles(MaterialTrack track, IReadOnlyList<TrackAggregate> profiles)
    {
        var referenceScalars = profiles.Sum(profile => profile.ReferenceScalars);
        var distance = profiles.Sum(profile => profile.ScalarDistance);
        var cer = referenceScalars == 0 ? (distance == 0 ? 0.0 : 1.0) : (double)distance / referenceScalars;
        return new TrackAggregate(
            track,
            "(all profiles)",
            profiles.Sum(profile => profile.Lines),
            referenceScalars,
            distance,
            cer)
        {
            Profiles = profiles,
        };
    }

    private static TrackAggregate Build(MaterialTrack track, string profile, IEnumerable<TrackLineScore> lines)
    {
        var list = lines.ToArray();
        var referenceScalars = list.Sum(line => line.Score.ReferenceScalarLength);
        var distance = list.Sum(line => line.Score.ScalarDistance);
        var cer = referenceScalars == 0 ? (distance == 0 ? 0.0 : 1.0) : (double)distance / referenceScalars;
        return new TrackAggregate(track, profile, list.Length, referenceScalars, distance, cer);
    }
}
