using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using static TorchSharp.torch;

namespace LeviathanText.Recognition;

/// <summary>A grapheme aligned to CTC time steps. Time steps refer to the model's
/// downsampled sequence, not source pixels; mapping back to pixels is the caller's job.</summary>
public sealed record AlignedGrapheme(
    string Text,
    int StartScalar,
    int EndScalar,
    int StartTimeStep,
    int EndTimeStep,
    Confidence Confidence);

public sealed record ForcedAlignmentResult(
    string Text,
    IReadOnlyList<AlignedGrapheme> Graphemes,
    int TimeSteps);

/// <summary>
/// CTC forced alignment for one known line text: a Viterbi pass over the blank/label
/// state graph. It requires a target that the label set can encode and enough time steps
/// for the target. This is an alignment primitive, not a language-model correction.
/// </summary>
public static class CtcForcedAligner
{
    public static ForcedAlignmentResult Align(
        CrnnModel model,
        LabelSet labels,
        LineImage image,
        string targetText)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(targetText);
        if (model.NumClasses != labels.NumClasses)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible,
                Scope = nameof(CtcForcedAligner),
                Message = "Model class count does not match the label set.",
            });

        var targetIndices = labels.EncodeText(targetText);
        if (targetIndices.Length == 0)
            return new ForcedAlignmentResult("", [], 0);

        var (images, lengths) = LineBatcher.ToTensor([image], model.WidthDownsampleFactor, model.InputHeightPixels);
        Tensor logProbs;
        using (var _ = no_grad())
            logProbs = model.forward(images);

        int timeSteps = Math.Min((int)logProbs.shape[0], lengths[0]);
        int classes = (int)logProbs.shape[2];
        var logits = logProbs.cpu().data<float>().ToArray();
        int stateCount = targetIndices.Length * 2 + 1;
        if (timeSteps < targetIndices.Length)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = nameof(CtcForcedAligner),
                Details = new Dictionary<string, string>
                {
                    ["timeSteps"] = timeSteps.ToString(),
                    ["targetLabels"] = targetIndices.Length.ToString(),
                },
                Message = "The target is too long for the model's time-step budget.",
            });

        var score = new double[stateCount, timeSteps];
        var back = new int[stateCount, timeSteps];
        for (int s = 0; s < stateCount; s++)
            for (int t = 0; t < timeSteps; t++)
            {
                score[s, t] = double.NegativeInfinity;
                back[s, t] = -1;
            }

        double Log(int t, int state) => logits[(t * classes) + StateClass(state, targetIndices)];

        score[0, 0] = Log(0, 0);
        if (stateCount > 1) score[1, 0] = Log(0, 1);

        for (int t = 1; t < timeSteps; t++)
        {
            for (int s = 0; s < stateCount; s++)
            {
                double best = score[s, t - 1];
                int previous = s;
                if (s > 0 && score[s - 1, t - 1] > best)
                {
                    best = score[s - 1, t - 1];
                    previous = s - 1;
                }
                // A label can skip a blank only when the two labels differ.
                if (s > 1 && s % 2 == 1 && targetIndices[s / 2] != targetIndices[(s - 2) / 2]
                    && score[s - 2, t - 1] > best)
                {
                    best = score[s - 2, t - 1];
                    previous = s - 2;
                }
                if (double.IsNegativeInfinity(best)) continue;
                score[s, t] = best + Log(t, s);
                back[s, t] = previous;
            }
        }

        int finalState = stateCount - 1;
        if (stateCount > 1 && score[stateCount - 2, timeSteps - 1] > score[finalState, timeSteps - 1])
            finalState = stateCount - 2;
        if (double.IsNegativeInfinity(score[finalState, timeSteps - 1]))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = nameof(CtcForcedAligner),
                Message = "No CTC alignment path exists for the target.",
            });

        var path = new int[timeSteps];
        int state = finalState;
        for (int t = timeSteps - 1; t >= 0; t--)
        {
            path[t] = state;
            state = back[state, t] >= 0 ? back[state, t] : state;
        }

        var spans = new List<AlignedGrapheme>();
        var enumerated = Graphemes.Enumerate(targetText).ToList();
        int scalarCursor = 0;
        for (int label = 0; label < targetIndices.Length; label++)
        {
            int ctcState = label * 2 + 1;
            int first = Array.IndexOf(path, ctcState);
            int last = Array.LastIndexOf(path, ctcState);
            if (first < 0)
                throw new InvalidOperationException("Aligned path lost a target label.");
            double confidence = 0;
            int count = 0;
            for (int t = first; t <= last; t++)
            {
                confidence += Math.Exp(Log(t, ctcState));
                count++;
            }
            var grapheme = enumerated[label];
            int scalars = ScalarText.ScalarLength(grapheme.Text);
            spans.Add(new AlignedGrapheme(
                grapheme.Text,
                scalarCursor,
                scalarCursor + scalars,
                first,
                last,
                Confidence.Of(Math.Clamp(confidence / Math.Max(1, count), 0, 1), ConfidenceScale.RawModelProbability)));
            scalarCursor += scalars;
        }

        return new ForcedAlignmentResult(targetText, spans, timeSteps);
    }

    private static int StateClass(int state, IReadOnlyList<int> target) =>
        state % 2 == 0 ? LabelSet.BlankIndex : target[state / 2];
}
