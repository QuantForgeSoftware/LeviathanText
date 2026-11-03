using LeviathanText.Contracts;
using static TorchSharp.torch;

namespace LeviathanText.Recognition;

/// <summary>
/// Runs a <see cref="CrnnModel"/> and decodes CTC output into L0 text with per-grapheme
/// confidence. Greedy decoding is the default; prefix beam search is opt-in
/// (<see cref="CtcDecodeMode.PrefixBeam"/>) because its benefit is a measurement, not an
/// assumption. Confidence values are raw model probabilities interpreted along the chosen
/// alignment and are never presented as scholarly certainty.
/// </summary>
public sealed class TorchLineRecognizer : ICtcRecognizer
{
    public TorchLineRecognizer(CrnnModel model, LabelSet labels, CtcDecodeMode mode = CtcDecodeMode.Greedy, int beamWidth = 8)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(labels);
        if (beamWidth < 1) throw new ArgumentOutOfRangeException(nameof(beamWidth), "Beam width must be at least 1.");

        Model = model;
        Labels = labels;
        Mode = mode;
        BeamWidth = beamWidth;
    }

    public CrnnModel Model { get; }
    public LabelSet Labels { get; }
    public CtcDecodeMode Mode { get; }
    public int BeamWidth { get; }

    /// <summary>Infer in evaluation mode, restoring the caller's model mode even on failure.</summary>
    public IReadOnlyList<RecognitionResult> Recognize(IReadOnlyList<LineImage> lines)
    {
        if (lines.Count == 0) return [];

        bool wasTraining = Model.training;
        try
        {
            Model.eval();
            return RecognizeInEvaluationMode(lines);
        }
        finally
        {
            Model.train(wasTraining);
        }
    }

    private IReadOnlyList<RecognitionResult> RecognizeInEvaluationMode(IReadOnlyList<LineImage> lines)
    {
        // Decoding returns managed values; all forward intermediates can end here.
        using var tensorScope = NewDisposeScope();
        if (Model.NumClasses != Labels.NumClasses)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible,
                Scope = nameof(TorchLineRecognizer),
                Details = new Dictionary<string, string>
                {
                    ["modelClasses"] = Model.NumClasses.ToString(),
                    ["labelClasses"] = Labels.NumClasses.ToString(),
                },
                Message = "Model class count does not match the label set.",
            });

        var (images, lengths) = LineBatcher.ToTensor(lines, Model.WidthDownsampleFactor, Model.InputHeightPixels);
        using var input = images;

        Tensor logProbs;
        using (var _ = no_grad())
        {
            logProbs = Model.forward(images);
        }

        using (logProbs)
            return CtcOutputDecoder.Decode(logProbs, lengths, Labels, Mode, BeamWidth);
    }
}
