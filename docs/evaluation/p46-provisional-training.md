# P46 provisional recognition training — 2026-11-02

The owner authorized training after preparation of the [specialist packet](../data/p46-specialist-review-packet.md). This is a local C# CRNN/CTC experiment using unadjudicated source-reference labels. It does not satisfy the reviewed-corpus or release gates in [the pilot plan](../../project-plan.md#12-ntvmr--csntm-manuscript-pairing-pilot--2026-11-02).

## Data and scope

Use the retained official University of Michigan Library Digital Collections width-2200 images of P.Mich.inv. 6238, APIS3612/3613/3614, paired with INTF NTVMR docID 10046 TEI for pages 1440/1450/1460. [Item-level notices and the crosswalk](../data/p46-michigan-page-review.md) identify the images as public domain/PDM1.0 and the actual TEI as CC BY 4.0. Original notices and attribution remain in the source packet. These experiments assign no distribution license to weights and do not cover separate CSNTM photographs.

| Partition | Physical unit | Pages | Lines | Reference Unicode scalars |
| --- | --- | --- | ---: | ---: |
| Training | P46 leaf 82, both surfaces |1450,1460|17|578|
| Development / checkpoint selection | P46 leaf 81, verso |1440|7|219|
| Blind test | none |none|0|0|

The same witness and probably the same hand occur across partitions. All three pages were previously inspected; this is a within-witness development diagnostic. It measures neither unseen-hand/unseen-witness performance nor Classical Greek coverage. There is no language model, AI correction, standard-edition substitution or transfer from a pretrained checkpoint.

Exclude every source line with any flag, including supplied, unclear, apparatus, abbreviation and continued-word flags, and every uncertain terminal association. Also exclude plain-source lower rows 1460 L16/L17/L19/L20 after visual crop QA found clipping, tears or neighboring ink. The final 24-line subset is fixed across both runs; 57 of the 81 proposed packet records are excluded. No source restoration or abbreviation expansion is inserted into a label.

Targets remove editorial whitespace, use NFD, and retain the source's lowercase spelling and separate `σ`/`ς`. These conventions are reference encodings, not scholarly assertions about letter case, word division or visible lunate-sigma identity. All 81 specialist decisions remain pending; no accepted `visible_text` fields were populated.

**Later package-provenance correction:** the copied `SOURCE-ATTRIBUTION.md` describes initial packet crops as unaltered rectangles, which does not describe the rectified training inputs below. [The corrective record](p46-source-provenance-correction.md) must accompany transferred research packages; historical artifact files and hashes remain preserved.

## Preparation and fixed protocol

Preparation and training use C# against existing LeviathanText libraries. The existing CLI's automatic three-page split would separate the two surfaces of leaf 82; its bounding-box crop does not straighten sloping lines. The separate experiment runners therefore use explicit leaf membership and new experimental crops.

Initial 56 px proposed-center crops clipped some letters. Final crops are 64 px high and follow the packet's proposed slopes, refined by ink centroids in 100 px windows (luminance cutoff 0.22, prior window ±31 px, center adjustment bounded ±20 px), interpolated between windows. They use nearest-neighbor vertical column translation in the retained width-2200 frame. This geometry remains an AI-reviewed proposal. It is not an independently adjudicated original-resolution polygon.

Resize each crop proportionally to height 32 and convert to grayscale luminance, black 0 / white 1. Input-image QA rejected per-line global Otsu because it rendered the papyrus body almost solid black; thresholded inputs were never trained. Actual grayscale inputs and the final contact sheet were visually checked. This preparation differs from the generic PAGE loader; do not evaluate these packages through ordinary `evaluate lines` or load every file in an artifact folder as a dataset.

Use a train-only alphabet (25 Greek labels + CTC blank). Development contains no unknown labels. For every selected line require `floor(inputWidth/4) >= targetLength + adjacentRepeatedLabels` before fitting, so impossible alignments cannot disappear behind CTC's `zero_infinity` setting.

Both runs initialize from scratch with Torch seed 42 set **before** model construction, CPU threads 2, hidden size 64, height 32, width downsample 4, dropout 0, batch 4, Adam learning rate 0.003, weight decay 0 and no augmentation. v1 has 120 epochs; its selected checkpoint emits blank text and recorded development CER remains 1.0. v2 then has a separately fixed 1,200 epochs. This is a disclosed budget extension on the same development material, not a fresh acceptance evaluation. Seed equality does not establish bit reproducibility on all CPU/runtime configurations; no seed-spread accuracy claim is made.

## Results

| Measurement | Untrained | v1 selected | v2 selected | v2 final epoch |
| --- | ---: | ---: | ---: | ---: |
| Epoch |0|1 of 120|1,164 of 1,200|1,200|
| Training CER |99.48%|100.00%|**11.59% (67/578)**|9.52% (55/578)|
| Development CER |100.00%|100.00%|**73.97% (162/219)**|78.08% (171/219)|
| Training evaluation loss |16.3198|6.1008|0.4503|0.3501|
| Development evaluation loss |17.5999|6.3938|4.4723|4.5409|

v1 completed 120 epochs in 15.52 seconds; development CER equals 1.0 at every recorded epoch, so the earliest tied checkpoint (epoch 1) was selected. Its saved predictions are all blank; intermediate decoded strings were not retained. v2 completed 1,200 epochs in 144.20 seconds; development CER first fell below 1.0 at epoch 666. v2's first 120 history records exactly match v1 on this machine. The selected v2 checkpoint emits nonempty text for all 24 lines. Training fit improved substantially while development recognition remained poor, with a large train/development gap. Approximately 74% CER is not adequate for reliable transcription. The small development sample also has source/geometry uncertainty and was used for budget extension and checkpoint selection.

[P46 v2 learning curves (PNG)](../../.artifacts/experiments/p46-provisional-v2/learning-curves.png)

This is a comparison against random initialization and the shorter diagnostic, not against the project's existing model or Kraken/eScriptorium. No product replacement, regression, acceptance-threshold or general Greek accuracy claim follows.

Both best checkpoints were restored before measurement, packaged and reloaded through `RecognitionModelLoader`; decoded strings matched on all 24 lines. An independent audit recomputed aggregate scalar edit counts from saved per-line predictions, checked checkpoint epoch/selected CER, and verified the selected model digest against its manifest. The v2 selected weight SHA-256 is `2ab0f93c3bae03c2fd6dad5a833092e7ae764b2923a21083bfe3510763a3f9c0`. Preparation/training built and ran successfully after obsolete Skia call warnings were corrected. No production application source changed, and the full application test suite was not rerun for this artifact-only experiment.

## Local artifacts and reproduction

Both experiments are under the Git-ignored `.artifacts/experiments/p46-provisional-v1/` and `p46-provisional-v2/` folders. Each retains `Program.cs`, `Experiment.csproj`, `protocol.json`, `line-ledger.json`, `crop-contact.png`, selected crops and exact input images, `baseline.json`, `results.json`, per-line `predictions.json`, latest/best checkpoints, learning curves and a packaged selected model with manifest/card/source attribution. `experiment-receipt.json` hashes the runner, protocol, ledger and results and records repository HEAD, SDK 10.0.301 and arm64 architecture. Training timings exclude preparation/build/packaging; peak memory was not measured. v1's four rejected lower-row assets are retained separately in `qa-rejected/` and were not used in training. Packages are local research artifacts, not published releases. The trained C# pipeline did not invoke Python; Python was used afterward for evidence verification and report plots.

Run from the repository root, with .NET 10 and the project's cached/restorable TorchSharp/Skia dependencies. The runner takes an **absolute repository path** followed by `prepare` or `train`:

```sh
dotnet restore .artifacts/experiments/p46-provisional-v2/Experiment.csproj -p:NuGetAudit=false
dotnet run --no-restore --project .artifacts/experiments/p46-provisional-v2/Experiment.csproj -- /absolute/path/to/LeviathanText prepare
dotnet run --no-restore --project .artifacts/experiments/p46-provisional-v2/Experiment.csproj -- /absolute/path/to/LeviathanText train
```

Preserve the existing artifact folder before a reproduction: training refuses to overwrite its checkpoint directory. `prepare` regenerates preparation artifacts, so use a copy for comparison. Keep the relative experiment/source paths when reproducing. The source packet supplies exact local source files and manifests; it is not refetched at runtime.

## Remaining work

The [development-error investigation](p46-development-error-investigation.md) quantifies under-emission, reports an unsuccessful beam-decoder remedy, audits appearance/pipeline issues and links the concrete next-run protocol.

Specialist decisions, independent geometry review, a declared diplomatic target state/mark policy, the larger 10–20-page/50–100-reviewed-line sample, fresh acceptance pages, regression evaluation on previously supported profiles and an unseen witness remain open. This small run does not justify replacing the existing default model or deploying automated transcription. Keep corpus work and training implementation independently owned; hand accepted versioned line revisions, geometry, hashes and leaf IDs from the corpus instance to the training instance before a larger experiment.
