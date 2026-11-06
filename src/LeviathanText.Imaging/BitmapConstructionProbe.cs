using SkiaSharp;

namespace LeviathanText.Imaging;

internal enum BitmapConstructionStage { DestinationAllocated, CanvasCreated, NativeInitialized, BeforePublish }
internal delegate void BitmapConstructionProbe(BitmapConstructionStage stage, SKBitmap destination, SKCanvas? canvas);
