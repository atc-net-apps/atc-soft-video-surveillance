namespace AtcSoft.VideoEngine;

/// <summary>
/// Factory for creating GPU accelerator instances.
/// Returns <c>null</c> when GPU acceleration is not available.
/// </summary>
public interface IGpuAcceleratorFactory
{
    /// <summary>
    /// Attempts to create a GPU accelerator.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>A GPU accelerator, or <c>null</c> if unavailable.</returns>
    IGpuAccelerator? TryCreate(ILogger logger);
}