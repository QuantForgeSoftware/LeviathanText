using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;

namespace LeviathanText.Recognition;

/// <summary>
/// A convolutional-recurrent network with a CTC output head for unsegmented line
/// recognition. Input is (N, 1, 16, W) grayscale; output is log-probabilities
/// (T, N, C) where T = floor(W / 4). Fixed input height and a width downsample factor
/// of 4 keep the acoustic-length bookkeeping simple and testable.
///
/// CPU-only and dependency-light: the architecture is deliberately small so the
/// Phase 0 feasibility gate (train / reload / infer) runs without a GPU.
/// </summary>
public sealed class CrnnModel : nn.Module<Tensor, Tensor>
{
    public const int InputHeight = 16;

    /// <summary>Output time steps per input pixel column.</summary>
    public const int WidthDownsampleFactor = 4;

    private const int ConvChannels = 32;
    private const int FeatureHeightAfterPools = InputHeight / WidthDownsampleFactor; // two 2x2 pools -> 4

    private readonly nn.Module<Tensor, Tensor> _conv1;
    private readonly nn.Module<Tensor, Tensor> _conv2;
    private readonly nn.Module<Tensor, Tensor> _pool;
    private readonly LSTM _lstm;
    private readonly nn.Module<Tensor, Tensor> _fc;

    public CrnnModel(long numClasses, long hiddenSize = 64) : base(nameof(CrnnModel))
    {
        if (numClasses < 2) throw new ArgumentOutOfRangeException(nameof(numClasses), "Need at least a blank plus one label.");
        NumClasses = numClasses;
        HiddenSize = hiddenSize;

        _conv1 = nn.Conv2d(1, ConvChannels / 2, 3, padding: 1);
        _conv2 = nn.Conv2d(ConvChannels / 2, ConvChannels, 3, padding: 1);
        _pool = nn.MaxPool2d(2, 2);
        _lstm = nn.LSTM(inputSize: ConvChannels * FeatureHeightAfterPools, hiddenSize: hiddenSize, numLayers: 1, batchFirst: false);
        _fc = nn.Linear(hiddenSize, numClasses);

        register_module(nameof(_conv1), _conv1);
        register_module(nameof(_conv2), _conv2);
        register_module(nameof(_pool), _pool);
        register_module(nameof(_lstm), _lstm);
        register_module(nameof(_fc), _fc);
    }

    public long NumClasses { get; }
    public long HiddenSize { get; }

    public override Tensor forward(Tensor input)
    {
        if (input.dim() != 4) throw new ArgumentException("Expected a 4-D (N,1,H,W) tensor.", nameof(input));
        if (input.shape[2] != InputHeight)
            throw new ArgumentException($"Expected input height {InputHeight}, got {input.shape[2]}.", nameof(input));

        var x = _conv1.forward(input).relu();
        x = _pool.forward(x);
        x = _conv2.forward(x).relu();
        x = _pool.forward(x);

        long n = x.shape[0], c = x.shape[1], h = x.shape[2], w = x.shape[3];
        // (N, C, H, W) -> (W, N, C*H) so the LSTM runs along the width axis.
        x = x.reshape(n, c * h, w).permute(2, 0, 1);

        var (output, _, _) = _lstm.forward(x);
        var logits = _fc.forward(output); // (T, N, C)
        return nn.functional.log_softmax(logits, dim: 2);
    }

    /// <summary>Number of CTC time steps for a given pixel width.</summary>
    public static int TimeStepsFor(int width) => Math.Max(1, width / WidthDownsampleFactor);
}
