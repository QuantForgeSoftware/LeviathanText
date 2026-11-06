namespace LeviathanText.Infrastructure;

/// <summary>A principal lost project-write access before the atomic publication point.</summary>
public sealed class ProjectWriteAccessDeniedException() : Exception(
    "Current account or project access does not permit this project write.");
