using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class LegacyHeightRegressionTests
{
    [Fact]
    public void Existing_task_constructor_uses_custom_model_height()
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32);
        using var page = new SKBitmap(80, 48);
        page.Erase(SKColors.White);
        var line = new NativeLine
        {
            Id = new(EntityKind.Line, "line"), RegionId = new(EntityKind.Region, "region"),
            FrameRef = new(EntityKind.Capture, "capture"),
            Polygon = [new(8, 8), new(72, 8), new(72, 40), new(8, 40)],
        };
        Assert.Single(new RecognitionTaskModel(model, labels).Predict(page, [line]));
    }
}
