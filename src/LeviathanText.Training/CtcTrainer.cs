using LeviathanText.Contracts;
using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using TorchSharp;
using static TorchSharp.torch;

namespace LeviathanText.Training;

/// <summary>
/// Minimal but complete CTC training loop: variable-width batching, CTC loss, best
/// checkpoint selection by validation CER, early stopping, and optimizer-state resume.
///
/// This is deliberately the smallest thing that satisfies the Phase 0 C# gate
/// (train / resume / reload / infer). It is not a hyper-parameter search framework and
/// makes no accuracy claim; it runs on synthetic data until real lines are available.
/// </summary>
public sealed class CtcTrainer
{
    private readonly CrnnModel _model;
    private readonly LabelSet _labels;
    private readonly TrainingConfig _config;
    private readonly TorchSharp.Modules.Adam _optimizer;

    private long _globalStep;
    private double _bestValidationCer = double.PositiveInfinity;
    private int _epoch;

    public CtcTrainer(CrnnModel model, LabelSet labels, TrainingConfig config)
    {
        _model = model;
        _labels = labels;
        _config = config;
        _optimizer = optim.Adam(model.parameters(), lr: config.LearningRate);
        manual_seed(config.Seed);
    }

    public int Epoch => _epoch;
    public long GlobalStep => _globalStep;
    public double BestValidationCer => _bestValidationCer;
    public TrainingConfig Config => _config;

    /// <summary>Average CTC loss over one pass, training the model.</summary>
    public double TrainEpoch(IReadOnlyList<LabeledLine> data, int epochSeed)
    {
        var rng = new Random(epochSeed);
        var shuffled = data.OrderBy(_ => rng.Next()).ToList();

        double total = 0;
        int batches = 0;
        for (int i = 0; i < shuffled.Count; i += _config.BatchSize)
        {
            var batch = shuffled.Skip(i).Take(_config.BatchSize).ToList();
            var collated = VariableWidthCollator.Collate(batch, _labels, _model.WidthDownsampleFactor, _model.InputHeightPixels);

            _optimizer.zero_grad();
            var logProbs = _model.forward(collated.Images);
            var loss = TorchSharp.torch.nn.functional.ctc_loss(
                logProbs, collated.Targets, collated.InputLengths, collated.TargetLengths,
                blank: LabelSet.BlankIndex, zero_infinity: true);
            loss.backward();
            _optimizer.step();

            total += loss.item<float>();
            batches++;
            _globalStep++;
        }

        return batches == 0 ? 0 : total / batches;
    }

    /// <summary>Average CTC loss and micro character error rate over a set.</summary>
    public (double Loss, double Cer) Evaluate(IReadOnlyList<LabeledLine> data)
    {
        double lossTotal = 0;
        int batches = 0;

        using (var _ = no_grad())
        {
            for (int i = 0; i < data.Count; i += _config.BatchSize)
            {
                var batch = data.Skip(i).Take(_config.BatchSize).ToList();
                var collated = VariableWidthCollator.Collate(batch, _labels, _model.WidthDownsampleFactor, _model.InputHeightPixels);
                var logProbs = _model.forward(collated.Images);
                var loss = TorchSharp.torch.nn.functional.ctc_loss(
                    logProbs, collated.Targets, collated.InputLengths, collated.TargetLengths,
                    blank: LabelSet.BlankIndex, zero_infinity: true);
                lossTotal += loss.item<float>();
                batches++;
            }
        }

        var recognizer = new TorchLineRecognizer(_model, _labels);
        var results = recognizer.Recognize([.. data.Select(d => d.Image)]);

        int distance = 0;
        int referenceLength = 0;
        for (int i = 0; i < data.Count; i++)
        {
            var score = RecognitionScorer.Score(data[i].Text, results[i].Text);
            distance += score.ScalarDistance;
            referenceLength += score.ReferenceScalarLength;
        }

        double cer = referenceLength == 0 ? 0 : (double)distance / referenceLength;
        return (batches == 0 ? 0 : lossTotal / batches, cer);
    }

    public TrainingRunResult Fit(IReadOnlyList<LabeledLine> train, IReadOnlyList<LabeledLine> validation)
    {
        Directory.CreateDirectory(_config.CheckpointDirectory);

        int startEpoch = _epoch;
        int sinceImprovement = 0;
        var history = new List<EpochRecord>();
        var bestPath = string.Empty;

        for (int e = _epoch; e < _config.Epochs; e++)
        {
            double trainLoss = TrainEpoch(train, _config.Seed + e);
            var (valLoss, valCer) = Evaluate(validation);
            _epoch = e + 1;

            history.Add(new EpochRecord(_epoch, trainLoss, valLoss, valCer));

            bool improved = valCer < _bestValidationCer - 1e-6;
            if (improved)
            {
                _bestValidationCer = valCer;
                sinceImprovement = 0;
            }
            else
            {
                sinceImprovement++;
            }

            bestPath = SaveCheckpoint(isBest: improved);

            if (sinceImprovement >= _config.EarlyStoppingPatience) break;
        }

        return new TrainingRunResult(startEpoch, _epoch, _bestValidationCer, bestPath, history);
    }

    /// <summary>Save the current model + optimizer state and metadata; also the ".best" copy when requested.</summary>
    public string SaveCheckpoint(bool isBest)
    {
        Directory.CreateDirectory(_config.CheckpointDirectory);
        var name = _config.ModelName;

        SaveArtifacts(name, suffix: string.Empty);
        if (isBest)
        {
            SaveArtifacts(name, suffix: ".best");
            return CheckpointStore.ModelPath(_config.CheckpointDirectory, name, ".best");
        }

        return CheckpointStore.ModelPath(_config.CheckpointDirectory, name);
    }

    private void SaveArtifacts(string name, string suffix)
    {
        _model.save(CheckpointStore.ModelPath(_config.CheckpointDirectory, name, suffix));
        _optimizer.save_state_dict(CheckpointStore.OptimizerPath(_config.CheckpointDirectory, name, suffix));
        CheckpointStore.WriteMetadata(_config.CheckpointDirectory, name, new CheckpointMetadata
        {
            Epoch = _epoch,
            GlobalStep = _globalStep,
            BestValidationCer = _bestValidationCer,
            LabelSetHash = CheckpointStore.LabelSetHash(_labels),
            Labels = _labels.Labels,
            ConfigHash = _config.ComputeHash(),
            TorchVersion = torch.__version__,
            CreatedAt = DateTimeOffset.UtcNow,
        }, suffix);
    }

    /// <summary>Restore model, optimizer and counters from a checkpoint (latest or ".best").</summary>
    public CheckpointMetadata LoadCheckpoint(string suffix = "")
    {
        var name = _config.ModelName;
        var metadata = CheckpointStore.ReadMetadata(_config.CheckpointDirectory, name, suffix);

        // The label set travels with the model (domain-and-pipeline.md §3). A same-size
        // but different vocabulary would otherwise decode confidently into wrong letters.
        var expectedLabels = CheckpointStore.LabelSetHash(_labels);
        if (!string.Equals(metadata.LabelSetHash, expectedLabels, StringComparison.Ordinal))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible,
                Scope = name,
                Details = new Dictionary<string, string>
                {
                    ["checkpointLabels"] = metadata.LabelSetHash,
                    ["currentLabels"] = expectedLabels,
                },
                RemedyHint = RemedyHint.MapUnknownLabel,
                Message = "Checkpoint label set does not match the label set supplied for loading.",
            });

        _model.load(CheckpointStore.ModelPath(_config.CheckpointDirectory, name, suffix));
        _optimizer.load_state_dict(CheckpointStore.OptimizerPath(_config.CheckpointDirectory, name, suffix));

        _epoch = metadata.Epoch;
        _globalStep = metadata.GlobalStep;
        _bestValidationCer = metadata.BestValidationCer;
        return metadata;
    }
}
