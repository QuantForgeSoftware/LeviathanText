using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
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

    public IReadOnlyList<RecognitionResult> Recognize(IReadOnlyList<LineImage> lines)
    {
        if (lines.Count == 0) return [];

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

        var (images, lengths) = LineBatcher.ToTensor(lines);

        Tensor logProbs;
        using (var _ = no_grad())
        {
            logProbs = Model.forward(images);
        }

        long timeSteps = logProbs.shape[0];
        long batch = logProbs.shape[1];
        long classes = logProbs.shape[2];
        if (timeSteps > int.MaxValue || classes != Labels.NumClasses)
            throw new InvalidOperationException("Unexpected model output shape.");

        var probabilities = logProbs.exp().cpu().data<float>().ToArray();
        var argmax = logProbs.argmax(2).cpu().data<long>().ToArray(); // row-major (T, N)
        var classCount = (int)classes;

        var results = new List<RecognitionResult>(lines.Count);
        for (int n = 0; n < lines.Count; n++)
        {
            int[] indices;
            List<double> stepConfidence;

            if (Mode == CtcDecodeMode.Greedy)
            {
                var greedy = new List<int>();
                stepConfidence = new List<double>();
                int previous = -1;
                for (int t = 0; t < lengths[n]; t++)
                {
                    int index = (int)argmax[t * batch + n];
                    if (index != previous && index != LabelSet.BlankIndex)
                    {
                        greedy.Add(index);
                        stepConfidence.Add(probabilities[(t * batch + n) * classes + index]);
                    }
                    previous = index;
                }
                indices = [.. greedy];
            }
            else
            {
                var matrix = new double[lengths[n]][];
                for (int t = 0; t < lengths[n]; t++)
                {
                    var row = new double[classCount];
                    for (int c = 0; c < classCount; c++)
                        row[c] = probabilities[(t * batch + n) * classes + c];
                    matrix[t] = row;
                }

                indices = CtcBeamDecoder.PrefixBeamSearch(matrix, BeamWidth);
                stepConfidence = CtcBeamDecoder.BestAlignment(matrix, indices).Select(a => a.Probability).ToList();
            }

            var text = Labels.Decode(indices);
            var spans = new List<GraphemeConfidence>();
            int k = 0;
            foreach (var grapheme in Graphemes.Enumerate(text))
            {
                double p = k < stepConfidence.Count ? stepConfidence[k] : 0.0;
                spans.Add(new GraphemeConfidence(
                    grapheme.StartScalar,
                    grapheme.StartScalar + ScalarText.ScalarLength(grapheme.Text),
                    Confidence.Of(Math.Clamp(p, 0.0, 1.0), ConfidenceScale.RawModelProbability)));
                k++;
            }

            results.Add(new RecognitionResult(text, indices, spans));
        }

        return results;
    }
}
