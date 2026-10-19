using Xunit;

// The ML suite loads native libtorch and several tests train models on real line crops.
// Running those classes in parallel caused a native test-host crash (observed 2026-10-19
// after the P01 experiment matrix grew). Serial execution is slower but stable; the
// dataset-backed tests skip automatically when the artifact store is absent.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
