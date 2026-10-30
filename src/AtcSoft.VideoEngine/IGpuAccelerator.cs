namespace AtcSoft.VideoEngine;

/// <summary>
/// Cross-platform interface for GPU-accelerated video decoding and rendering.
/// Implementations provide hardware device contexts for FFmpeg and handle
/// decoded frame processing (e.g., NV12→BGRA conversion).
/// </summary>
public unsafe interface IGpuAccelerator : IDisposable
{
    /// <summary>
    /// Gets the FFmpeg hardware device type (e.g., D3D11VA).
    /// </summary>
    AVHWDeviceType HwDeviceType { get; }

    /// <summary>
    /// Gets the FFmpeg hardware device context buffer for <c>AVCodecContext.hw_device_ctx</c>.
    /// </summary>
    AVBufferRef* HwDeviceContext { get; }

    /// <summary>
    /// Gets a value indicating whether the accelerator is initialized and ready.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Called on the demux thread after each frame is decoded.
    /// For D3D11VA, extracts the texture from the frame and runs GPU conversion.
    /// </summary>
    /// <param name="frame">The decoded frame (may contain GPU surface references).</param>
    void OnFrameDecoded(AVFrame* frame);

    /// <summary>
    /// Occurs on the demux thread after a frame has been decoded and processed.
    /// </summary>
    event Action? FrameReady;

    /// <summary>
    /// Captures the latest rendered frame as PNG bytes by reading back from GPU.
    /// </summary>
    /// <returns>PNG-encoded bytes, or <c>null</c> if no frame is available.</returns>
    byte[]? CaptureSnapshot();

    /// <summary>
    /// Sets the clockwise rotation applied to subsequent frames at the GPU
    /// video-processor stage. Display, snapshots, and the next-allocated
    /// output texture all reflect the new value.
    /// </summary>
    void SetRotation(VideoRotation rotation);
}