namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Receives one encoded packet. The packet belongs to the encoder and is
/// only valid during the call.
/// </summary>
internal unsafe delegate void EncodedPacketHandler(AVPacket* packet);