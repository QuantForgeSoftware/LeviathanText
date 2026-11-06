using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class CaptureFrameIdentityTests
{
    private static NativeProject WithFrame(EntityKind kind)
    {
        var project = SampleProjectBuilder.Build(); var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with { Frame = surface.Captures[0].Frame with { ImageRef = new EntityId(kind, "frame") } };
        return project with { Objects = [project.Objects[0] with { Surfaces = [surface with { Captures = [capture] }] }] };
    }

    [Theory]
    [InlineData(EntityKind.Collection)] [InlineData(EntityKind.Surface)] [InlineData(EntityKind.Region)]
    [InlineData(EntityKind.Line)] [InlineData(EntityKind.Model)]
    public void A_nonimage_entity_cannot_masquerade_as_the_capture_coordinate_frame(EntityKind kind)
    {
        var project = WithFrame(kind); var surface = project.Objects[0].Surfaces[0]; var before = NativeJson.Save(project);
        var error = Assert.Throws<LeviathanException>(() => CaptureDisplayService.ResolveCapture(project, surface.Id, surface.Captures[0].Id));
        Assert.Equal(ErrorCode.FrameUnregistered, error.Error.Code); Assert.Equal(before, NativeJson.Save(project));
    }

    [Theory]
    [InlineData(EntityKind.Capture)] [InlineData(EntityKind.Derivative)]
    public void Both_image_identity_kinds_remain_valid_without_requiring_capture_id_equality(EntityKind kind)
    {
        var project = WithFrame(kind); var surface = project.Objects[0].Surfaces[0];
        Assert.Equal(surface.Captures[0], CaptureDisplayService.ResolveCapture(project, surface.Id, surface.Captures[0].Id));
        Assert.NotEqual(surface.Captures[0].Id, surface.Captures[0].Frame.ImageRef);
    }
}
