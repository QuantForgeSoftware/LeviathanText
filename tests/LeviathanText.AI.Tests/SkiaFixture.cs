using SkiaSharp;
namespace LeviathanText.AI.Tests;
internal sealed class SkiaFixture(int width, int height, byte value = 0) : IDisposable
{
    private readonly SKBitmap bitmap = Make(width, height, value);
    private static SKBitmap Make(int width, int height, byte value)
    { var result = new SKBitmap(width, height); result.Erase(new SKColor(value,value,value)); return result; }
    public void SaveAsPng(Stream output) { using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Png,100); bytes.SaveTo(output); }
    public void SaveAsJpeg(Stream output) { using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Jpeg,100); bytes.SaveTo(output); }
    public void Dispose() => bitmap.Dispose();
}
