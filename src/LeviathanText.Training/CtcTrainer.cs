using LeviathanText.Contracts;
using LeviathanText.Evaluation;
using LeviathanText.Core.Unicode;
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
public sealed class CtcTrainer : IDisposable
{
    private readonly CrnnModel _model;
    private readonly LabelSet _labels;
    private readonly TrainingConfig _config;
    private readonly TorchSharp.Modules.Adam _optimizer;

    private long _globalStep;
    private double _bestValidationCer = double.PositiveInfinity;
    private int _epoch;
    private bool _disposed;

    public CtcTrainer(CrnnModel model, LabelSet labels, TrainingConfig config)
    {
        if (config.DiagnosticsEveryNEpochs < 0) throw new ArgumentOutOfRangeException(nameof(config.DiagnosticsEveryNEpochs));
        if (config.DiagnosticsSampleCount is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(config.DiagnosticsSampleCount));
        config.PreprocessingProfile?.Validate(model.InputHeightPixels);
        LineAugmenter.ValidateGammaRange(config.GammaMin, config.GammaMax);
        if (config.TargetValidationCer is { } target && (!double.IsFinite(target) || target < 0))
            throw new ArgumentOutOfRangeException(nameof(config.TargetValidationCer), "Target CER must be finite and nonnegative.");
        _model = model;
        _labels = labels;
        _config = config;
        _optimizer = optim.Adam(model.parameters(), lr: config.LearningRate, weight_decay: config.WeightDecay);
        manual_seed(config.Seed);
    }

    public int Epoch => _epoch;
    public long GlobalStep => _globalStep;
    public double BestValidationCer => _bestValidationCer;
    public TrainingConfig Config => _config;

    /// <summary>Release the trainer-owned optimizer; the caller retains ownership of the model.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _optimizer.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Path of the last emergency checkpoint written after an exception or cancellation,
    /// or null when the run did not fail. Load it with suffix <c>.failure</c>.
    /// </summary>
    public string? LastEmergencyCheckpointPath { get; private set; }

    /// <summary>Why the last emergency checkpoint could not be written, if it failed.</summary>
    public string? LastEmergencyCheckpointError { get; private set; }

    /// <summary>Average CTC loss over one pass, training the model.</summary>
    public double TrainEpoch(IReadOnlyList<LabeledLine> data, int epochSeed)
    {
        ThrowIfDisposed();
        bool wasTraining = _model.training;
        try
        {
            _model.train();
            return TrainEpochCore(data, epochSeed);
        }
        finally
        {
            _model.train(wasTraining);
        }
    }

    private double TrainEpochCore(IReadOnlyList<LabeledLine> data, int epochSeed)
    {
        var rng = new Random(epochSeed);
        var shuffled = data.OrderBy(_ => rng.Next()).ToList();

        double total = 0;
        int batches = 0;
        for (int i = 0; i < shuffled.Count; i += _config.BatchSize)
        {
            // Bound native tensor temporaries to this batch, including failed collation.
            using var tensorScope = NewDisposeScope();
            var batch = shuffled.Skip(i).Take(_config.BatchSize).ToList();
            if (_config.AugmentTraining)
            {
                batch = batch
                    .Select(line => line with
                    {
                        Image = LineAugmenter.Apply(
                            line.Image, rng,
                            _config.MaxHorizontalStretch,
                            _config.MaxVerticalShiftPx,
                            _config.SaltPepperFraction,
                            _config.GammaMin,
                            _config.GammaMax),
                    })
                    .ToList();
            }
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
        var result = EvaluateCore(data, collectDiagnostics: false);
        return (result.Loss, result.Cer);
    }

    /// <summary>Collect error types, valid-frame blank statistics and fixed sample strings without changing weights or caller mode.</summary>
    public TrainingEvaluation EvaluateDetailed(IReadOnlyList<LabeledLine> data) => EvaluateCore(data, collectDiagnostics: true);

    private TrainingEvaluation EvaluateCore(IReadOnlyList<LabeledLine> data, bool collectDiagnostics)
    {
        ThrowIfDisposed();
        bool wasTraining = _model.training;
        try
        {
            _model.eval();
            return EvaluateInEvaluationMode(data, collectDiagnostics);
        }
        finally
        {
            _model.train(wasTraining);
        }
    }

    private TrainingEvaluation EvaluateInEvaluationMode(IReadOnlyList<LabeledLine> data, bool collectDiagnostics)
    {
        double lossTotal = 0;
        int batches = 0;
        long validFrames = 0, blankFrames = 0;
        double blankMass = 0;
        long referenceLength = 0, predictionLength = 0, insertions = 0, deletions = 0, substitutions = 0;
        var samples = new List<TrainingPredictionSample>();

        using (var _ = no_grad())
        {
            for (int i = 0; i < data.Count; i += _config.BatchSize)
            {
                using var tensorScope = NewDisposeScope();
                var batch = data.Skip(i).Take(_config.BatchSize).ToList();
                var collated = VariableWidthCollator.Collate(batch, _labels, _model.WidthDownsampleFactor, _model.InputHeightPixels);
                var logProbs = _model.forward(collated.Images);
                var loss = TorchSharp.torch.nn.functional.ctc_loss(
                    logProbs, collated.Targets, collated.InputLengths, collated.TargetLengths,
                    blank: LabelSet.BlankIndex, zero_infinity: true);
                lossTotal += loss.item<float>();
                batches++;
                var lengths = batch.Select(line => _model.TimeSteps(line.Image.Width)).ToArray();
                var results = CtcOutputDecoder.Decode(logProbs, lengths, _labels);
                for (int n = 0; n < batch.Count; n++)
                {
                    var reference = CanonicalText.ToScalars(CanonicalText.Nfc(batch[n].Text));
                    var prediction = CanonicalText.ToScalars(CanonicalText.Nfc(results[n].Text));
                    var edits = EditScript.Compute(reference, prediction);
                    referenceLength += reference.Count;
                    predictionLength += prediction.Count;
                    insertions += edits.Insertions;
                    deletions += edits.Deletions;
                    substitutions += edits.Substitutions;
                    if (collectDiagnostics && samples.Count < _config.DiagnosticsSampleCount)
                        samples.Add(new TrainingPredictionSample(i + n, batch[n].Text, results[n].Text));
                }
                if (collectDiagnostics)
                {
                    using var probabilities = logProbs.exp();
                    using var cpu = probabilities.cpu();
                    var values = cpu.data<float>().ToArray();
                    int classes = _labels.NumClasses;
                    for (int n = 0; n < batch.Count; n++)
                    for (int t = 0; t < _model.TimeSteps(batch[n].Image.Width); t++)
                    {
                        int offset = (t * batch.Count + n) * classes;
                        int best = 0;
                        for (int c = 1; c < classes; c++)
                            if (values[offset + c] > values[offset + best]) best = c;
                        if (best == LabelSet.BlankIndex) blankFrames++;
                        blankMass += values[offset + LabelSet.BlankIndex];
                        validFrames++;
                    }
                }
            }
        }

        return new TrainingEvaluation(batches == 0 ? 0 : lossTotal / batches, new TrainingDiagnostics
        {
            Lines = data.Count, ReferenceScalars = referenceLength, PredictionScalars = predictionLength,
            Insertions = insertions, Deletions = deletions, Substitutions = substitutions,
            ValidFrames = validFrames, BlankArgmaxFrames = blankFrames,
            MeanBlankPosterior = validFrames == 0 ? 0 : blankMass / validFrames, Samples = samples,
        });
    }

    public TrainingRunResult Fit(IReadOnlyList<LabeledLine> train, IReadOnlyList<LabeledLine> validation)
    {
        ThrowIfDisposed();
        Directory.CreateDirectory(_config.CheckpointDirectory);

        int startEpoch = _epoch;
        int sinceImprovement = 0;
        var history = new List<EpochRecord>();
        var bestPath = CheckpointStore.ModelPath(_config.CheckpointDirectory, _config.ModelName, ".best");
        if (!File.Exists(bestPath)) bestPath = string.Empty; // resumed run with no new epoch yet

        try
        {
            for (int e = _epoch; e < _config.Epochs; e++)
            {
                double trainLoss = TrainEpoch(train, _config.Seed + e);
                bool scheduledDiagnostics = _config.DiagnosticsEveryNEpochs > 0
                    && ((e + 1) % _config.DiagnosticsEveryNEpochs == 0 || e + 1 == _config.Epochs);
                var evaluation = EvaluateCore(validation, scheduledDiagnostics);
                double valLoss = evaluation.Loss, valCer = evaluation.Cer;
                _epoch = e + 1;

                bool improved = valCer < _bestValidationCer - 1e-6
                    || (_config.TargetValidationCer is { } selectionTarget
                        && _bestValidationCer > selectionTarget && valCer <= selectionTarget);
                if (improved)
                {
                    _bestValidationCer = valCer;
                    sinceImprovement = 0;
                }
                else
                {
                    sinceImprovement++;
                }

                bool stopping = sinceImprovement >= _config.EarlyStoppingPatience
                    || (_config.TargetValidationCer is { } target && valCer <= target);
                bool recordDiagnostics = scheduledDiagnostics || (_config.DiagnosticsEveryNEpochs > 0 && stopping);
                if (recordDiagnostics && !scheduledDiagnostics) evaluation = EvaluateDetailed(validation);
                history.Add(new EpochRecord(_epoch, trainLoss, valLoss, valCer)
                {
                    GlobalStep = _globalStep,
                    TrainingEvaluation = recordDiagnostics ? EvaluateDetailed(train) : null,
                    ValidationEvaluation = recordDiagnostics ? evaluation : null,
                });

                var savedPath = SaveCheckpoint(isBest: improved);
                if (improved) bestPath = savedPath; // only the ".best" artifact is the selected model

                if (stopping) break;
            }
        }
        catch
        {
            // Preserve the partially trained state without hiding the original failure.
            // This is a separate artifact so a previously selected ".best" checkpoint is
            // never overwritten by a failed epoch.
            TrySaveEmergencyCheckpoint();
            throw;
        }

        return new TrainingRunResult(startEpoch, _epoch, _bestValidationCer, bestPath, history);
    }

    /// <summary>Save the current model + optimizer state and metadata; also the ".best" copy when requested.</summary>
    public string SaveCheckpoint(bool isBest)
    {
        ThrowIfDisposed();
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
            PreprocessingProfile = _config.PreprocessingProfile,
            GlobalStep = _globalStep,
            BestValidationCer = _bestValidationCer,
            LabelSetHash = CheckpointStore.LabelSetHash(_labels),
            Labels = _labels.Labels,
            ConfigHash = _config.ComputeHash(),
            HiddenSize = _model.HiddenSize,
            InputHeightPixels = _model.InputHeightPixels,
            WidthDownsampleFactor = _model.WidthDownsampleFactor,
            Dropout = _model.Dropout,
            TorchVersion = torch.__version__,
            CreatedAt = DateTimeOffset.UtcNow,
        }, suffix);
    }

    private void TrySaveEmergencyCheckpoint()
    {
        try
        {
            SaveArtifacts(_config.ModelName, ".failure");
            LastEmergencyCheckpointPath = CheckpointStore.ModelPath(
                _config.CheckpointDirectory, _config.ModelName, ".failure");
        }
        catch (Exception ex)
        {
            LastEmergencyCheckpointError = ex.Message;
            // The original training/interruption exception is the useful error. A failed
            // emergency save must not replace it.
        }
    }

    /// <summary>Restore the selected best checkpoint into this trainer's model/optimizer.</summary>
    public CheckpointMetadata RestoreBest() => LoadCheckpoint(".best");

    /// <summary>Restore model, optimizer and counters from a checkpoint (latest or ".best").</summary>
    public CheckpointMetadata LoadCheckpoint(string suffix = "")
    {
        ThrowIfDisposed();
        var name = _config.ModelName;
        var metadata = CheckpointStore.ReadMetadata(_config.CheckpointDirectory, name, suffix);
        try
        {
            metadata.PreprocessingProfile?.Validate(_model.InputHeightPixels);
            if (metadata.PreprocessingProfile != _config.PreprocessingProfile)
                throw new InvalidDataException("Checkpoint preprocessing profile does not match the training configuration; legacy metadata is not automatically migrated.");
        }
        catch (InvalidDataException ex)
        {
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible, Scope = name, RemedyHint = RemedyHint.ReRunStage,
                Message = ex.Message,
            });
        }

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

        if (metadata.InputHeightPixels != _model.InputHeightPixels
            || metadata.WidthDownsampleFactor != _model.WidthDownsampleFactor)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ModelIncompatible,
                Scope = name,
                Details = new Dictionary<string, string>
                {
                    ["checkpointInputHeight"] = metadata.InputHeightPixels.ToString(),
                    ["modelInputHeight"] = _model.InputHeightPixels.ToString(),
                    ["checkpointWidthDownsample"] = metadata.WidthDownsampleFactor.ToString(),
                    ["modelWidthDownsample"] = _model.WidthDownsampleFactor.ToString(),
                },
                RemedyHint = RemedyHint.ReRunStage,
                Message = "Checkpoint architecture does not match the model supplied for loading.",
            });

        _model.load(CheckpointStore.ModelPath(_config.CheckpointDirectory, name, suffix));
        _optimizer.load_state_dict(CheckpointStore.OptimizerPath(_config.CheckpointDirectory, name, suffix));

        _epoch = metadata.Epoch;
        _globalStep = metadata.GlobalStep;
        _bestValidationCer = metadata.BestValidationCer;
        return metadata;
    }
}
