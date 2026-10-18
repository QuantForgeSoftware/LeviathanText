using LeviathanText.Contracts;

namespace LeviathanText.Core.Frames;

/// <summary>
/// Tracks known coordinate frames and explicit registrations, enforcing the C0 rule:
/// geometry transfer between frames requires a registration; anything else fails with
/// <see cref="ErrorCode.FrameUnregistered"/> and remedy <see cref="RemedyHint.RegisterCaptures"/>
/// (domain-and-pipeline.md §1.2; interchange-and-api.md §2.1).
/// </summary>
public sealed class FrameRegistry
{
    private readonly Dictionary<(EntityId ImageRef, Checksum ImageChecksum), CoordinateFrame> _frames = new();
    private readonly HashSet<(CoordinateFrame From, CoordinateFrame To)> _registrations = new();

    /// <summary>Record a known frame. Registering the identical frame twice is a no-op.</summary>
    public CoordinateFrame Register(CoordinateFrame frame)
    {
        _frames[(frame.ImageRef, frame.ImageChecksum)] = frame;
        return frame;
    }

    public bool IsKnown(CoordinateFrame frame) => _frames.ContainsKey((frame.ImageRef, frame.ImageChecksum));

    /// <summary>Record a registration between two known frames (both directions).</summary>
    public void RecordRegistration(Registration registration)
    {
        if (!IsKnown(registration.From) || !IsKnown(registration.To))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.FrameUnregistered,
                Scope = registration.ToString(),
                Details = new Dictionary<string, string> { ["reason"] = "registration references an unknown frame" },
                Message = "Both frames must be registered before a registration between them.",
            });

        _registrations.Add((registration.From, registration.To));
        _registrations.Add((registration.To, registration.From)); // transfer is symmetric once evidence exists
    }

    /// <summary>True when geometry may be expressed/transferred from one frame to the other.</summary>
    public bool TransferAllowed(CoordinateFrame from, CoordinateFrame to)
    {
        if (from == to) return true;
        return _registrations.Contains((from, to));
    }

    /// <summary>Like <see cref="TransferAllowed"/> but fails loudly with the contract error.</summary>
    public void RequireTransfer(CoordinateFrame from, CoordinateFrame to)
    {
        if (!TransferAllowed(from, to))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.FrameUnregistered,
                Scope = $"{from} → {to}",
                Details = new Dictionary<string, string>
                {
                    ["from"] = from.ToString(),
                    ["to"] = to.ToString(),
                    ["reason"] = from.ImageRef == to.ImageRef
                        ? "same image reference but different checksum (re-capture or re-derivative)"
                        : "no registration recorded between these captures",
                },
                RemedyHint = RemedyHint.RegisterCaptures,
                Message = "Geometry does not transfer between unregistered coordinate frames.",
            });
    }
}
