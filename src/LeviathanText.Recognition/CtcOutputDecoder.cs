using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using static TorchSharp.torch;

namespace LeviathanText.Recognition;

/// <summary>Decode a bounded CTC minibatch without another model forward pass.</summary>
public static class CtcOutputDecoder
{
    /// <summary>
    /// Decode single-precision log probabilities in (time, batch, classes) order.
    /// Input lengths mask padding; the caller retains ownership of the tensor.
    /// </summary>
    public static IReadOnlyList<RecognitionResult> Decode(
        Tensor logProbs,
        IReadOnlyList<int> inputLengths,
        LabelSet labels,
        CtcDecodeMode mode = CtcDecodeMode.Greedy,
        int beamWidth = 8)
    {
        ArgumentNullException.ThrowIfNull(logProbs);
        ArgumentNullException.ThrowIfNull(inputLengths);
        ArgumentNullException.ThrowIfNull(labels);
        if (logProbs.dim() != 3)
            throw new ArgumentException("CTC output must have rank three.", nameof(logProbs));
        if (mode is not CtcDecodeMode.Greedy and not CtcDecodeMode.PrefixBeam)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (beamWidth < 1) throw new ArgumentOutOfRangeException(nameof(beamWidth));
        long timeSteps = logProbs.shape[0];
        long batch = logProbs.shape[1];
        long classes = logProbs.shape[2];
        if (timeSteps > int.MaxValue || batch != inputLengths.Count || classes != labels.NumClasses)
            throw new ArgumentException("Output shape must match the batch and label set.", nameof(logProbs));
        foreach (int length in inputLengths)
            if (length < 0 || length > timeSteps)
                throw new ArgumentOutOfRangeException(nameof(inputLengths), "Lengths must be between zero and the output time steps.");
        if (inputLengths.Count == 0) return [];

        using var probabilitiesTensor = logProbs.exp();
        using var cpuProbabilities = probabilitiesTensor.cpu();
        var probabilities = cpuProbabilities.data<float>().ToArray();
        using var argmaxTensor = logProbs.argmax(2);
        using var cpuArgmax = argmaxTensor.cpu();
        var argmax = cpuArgmax.data<long>().ToArray(); // row-major (T, N)
        var classCount = (int)classes;

        var results = new List<RecognitionResult>(inputLengths.Count);
        for (int n = 0; n < inputLengths.Count; n++)
        {
            if (inputLengths[n] == 0)
            {
                results.Add(new RecognitionResult(string.Empty, [], []));
                continue;
            }
            int[] indices;
            List<double> stepConfidence;

            if (mode == CtcDecodeMode.Greedy)
            {
                var greedy = new List<int>();
                stepConfidence = new List<double>();
                int previous = -1;
                for (int t = 0; t < inputLengths[n]; t++)
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
                var matrix = new double[inputLengths[n]][];
                for (int t = 0; t < inputLengths[n]; t++)
                {
                    var row = new double[classCount];
                    for (int c = 0; c < classCount; c++)
                        row[c] = probabilities[(t * batch + n) * classes + c];
                    matrix[t] = row;
                }

                indices = CtcBeamDecoder.PrefixBeamSearch(matrix, beamWidth);
                stepConfidence = CtcBeamDecoder.BestAlignment(matrix, indices).Select(a => a.Probability).ToList();
            }

            var text = labels.Decode(indices);
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
