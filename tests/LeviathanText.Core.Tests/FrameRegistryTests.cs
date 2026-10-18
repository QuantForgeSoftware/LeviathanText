using LeviathanText.Contracts;
using LeviathanText.Core.Frames;

namespace LeviathanText.Core.Tests;

public class FrameRegistryTests
{
    private static readonly Checksum Sum = new() { Algorithm = "sha256", Digest = new string('a', 64) };
    private static readonly Checksum OtherSum = new() { Algorithm = "sha256", Digest = new string('b', 64) };
    private static readonly AgentRef Agent = new() { Id = "imager", IsModel = false };

    private static CoordinateFrame Frame(EntityKind kind, string suffix, Checksum checksum) => new()
    {
        ImageRef = new EntityId(kind, suffix),
        ImageChecksum = checksum,
    };

    [Fact]
    public void Same_frame_transfer_is_always_allowed()
    {
        var registry = new FrameRegistry();
        var frame = Frame(EntityKind.Capture, "cap1", Sum);
        Assert.True(registry.TransferAllowed(frame, frame));
    }

    [Fact]
    public void Unregistered_transfer_requires_registration()
    {
        var registry = new FrameRegistry();
        var frontal = Frame(EntityKind.Capture, "frontal", Sum);
        var raking = Frame(EntityKind.Capture, "raking", OtherSum);
        registry.Register(frontal);
        registry.Register(raking);

        var ex = Assert.Throws<LeviathanException>(() => registry.RequireTransfer(frontal, raking));
        Assert.Equal(ErrorCode.FrameUnregistered, ex.Error.Code);
        Assert.Equal(RemedyHint.RegisterCaptures, ex.Error.RemedyHint);
    }

    [Fact]
    public void Explicit_registration_enables_transfer_in_both_directions()
    {
        var registry = new FrameRegistry();
        var frontal = Frame(EntityKind.Capture, "frontal", Sum);
        var raking = Frame(EntityKind.Capture, "raking", OtherSum);
        registry.Register(frontal);
        registry.Register(raking);
        registry.RecordRegistration(new Registration
        {
            From = frontal,
            To = raking,
            TransformDescription = "homography:v1#3f2a",
            EvidenceBy = Agent,
            CreatedAt = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.True(registry.TransferAllowed(frontal, raking));
        Assert.True(registry.TransferAllowed(raking, frontal));
    }

    [Fact]
    public void Re_captured_image_is_a_different_frame_even_with_same_entity_id()
    {
        var registry = new FrameRegistry();
        var original = Frame(EntityKind.Derivative, "der1", Sum);
        var recaptured = Frame(EntityKind.Derivative, "der1", OtherSum); // same entity, new pixels
        registry.Register(original);
        registry.Register(recaptured);
        registry.RecordRegistration(new Registration
        {
            From = original, To = recaptured, TransformDescription = "identity", EvidenceBy = Agent,
        });

        // A THIRD capture with yet another checksum is still unregistered.
        var third = Frame(EntityKind.Derivative, "der1", new Checksum { Algorithm = "sha256", Digest = new string('c', 64) });
        registry.Register(third);
        Assert.False(registry.TransferAllowed(original, third));
        Assert.Throws<LeviathanException>(() => registry.RequireTransfer(original, third));
    }

    [Fact]
    public void Registration_with_unknown_frame_fails_loudly()
    {
        var registry = new FrameRegistry();
        var known = Frame(EntityKind.Capture, "known", Sum);
        var unknown = Frame(EntityKind.Capture, "unknown", Sum);
        registry.Register(known);

        Assert.Throws<LeviathanException>(() => registry.RecordRegistration(new Registration
        {
            From = known, To = unknown, TransformDescription = "x", EvidenceBy = Agent,
        }));
    }

    [Fact]
    public void Duplicate_registration_is_idempotent()
    {
        var registry = new FrameRegistry();
        var a = Frame(EntityKind.Capture, "a", Sum);
        var b = Frame(EntityKind.Capture, "b", OtherSum);
        registry.Register(a);
        registry.Register(b);
        var registration = new Registration { From = a, To = b, TransformDescription = "x", EvidenceBy = Agent };
        registry.RecordRegistration(registration);
        registry.RecordRegistration(registration); // no throw, no duplicate semantics
        Assert.True(registry.TransferAllowed(a, b));
    }
}
