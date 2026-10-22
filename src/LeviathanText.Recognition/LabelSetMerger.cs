using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Recognition;

public enum LabelSetMergeMode
{
    /// <summary>Refuse a label set that differs from the existing one.</summary>
    Fail,
    /// <summary>Use only labels present in the new dataset.</summary>
    New,
    /// <summary>Keep existing labels and append new labels.</summary>
    Union,
}

/// <summary>
/// Codec resize for a CTC label set. <c>new</c> and <c>union</c> are explicit modes;
/// <c>fail</c> refuses an incompatible vocabulary. Optional NFD normalization is a
/// model-versioned choice, never a default diplomatic transformation.
/// </summary>
public static class LabelSetMerger
{
    public static LabelSet Resize(
        LabelSet existing,
        IEnumerable<string> newLabels,
        LabelSetMergeMode mode,
        bool normalizeNfd = false)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(newLabels);

        var incoming = Normalize(newLabels, normalizeNfd).Distinct(StringComparer.Ordinal).ToList();
        if (incoming.Count == 0)
            throw new ArgumentException("The new label set is empty.", nameof(newLabels));

        return mode switch
        {
            LabelSetMergeMode.Fail => Fail(existing, incoming),
            LabelSetMergeMode.New => new LabelSet(incoming),
            LabelSetMergeMode.Union => new LabelSet(
                existing.Labels.Concat(incoming).Distinct(StringComparer.Ordinal)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static LabelSet Fail(LabelSet existing, IReadOnlyList<string> incoming)
    {
        var existingLabels = existing.Labels.ToHashSet(StringComparer.Ordinal);
        var missing = incoming.Where(label => !existingLabels.Contains(label)).ToList();
        if (missing.Count > 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible,
                Scope = nameof(LabelSetMerger),
                Details = new Dictionary<string, string>
                {
                    ["missingLabels"] = string.Join(' ', missing.Take(8)),
                    ["missingCount"] = missing.Count.ToString(),
                },
                RemedyHint = RemedyHint.MapUnknownLabel,
                Message = "The new dataset contains labels absent from the existing model; use resize 'new' or 'union'.",
            });
        return existing;
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> labels, bool normalizeNfd) =>
        labels.Select(label => normalizeNfd ? label.Normalize(NormalizationForm.FormD) : label);
}
