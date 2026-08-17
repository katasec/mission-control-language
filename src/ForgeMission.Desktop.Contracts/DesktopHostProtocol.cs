using System.Buffers.Binary;
using System.Text;

namespace ForgeMission.Desktop.Contracts;

// The complete Supervisor <-> Host control contract. Deliberately tiny: two commands out, one event
// back, no acknowledgement, no listener, no options record, no generic event bus. Adding to it is an
// architecture decision, not an implementation detail — see
// docs/design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes.
public enum DesktopHostCommandKind : byte
{
    Navigate = 1,
    ShowFailure = 2,
}

public readonly record struct DesktopHostCommand(DesktopHostCommandKind Kind, string Payload);

public enum DesktopHostEventKind : byte
{
    RetryRequested = 1,
}

public readonly record struct DesktopHostEvent(DesktopHostEventKind Kind);

// One owner for the wire format, so the Supervisor and the Host cannot drift apart on framing.
// A frame is [kind: byte][UTF-8 payload byte count: Int32 little-endian][payload]; RetryRequested
// carries an empty payload. A null read result means the peer closed its end of the pipe — the
// caller decides what that means (for the Supervisor: the Host is gone; for the Host: the
// Supervisor is gone). An unknown kind byte throws rather than being skipped: a garbled control
// stream is a defect, not something to recover from silently.
public static class DesktopHostProtocol
{
    private const int HeaderLength = sizeof(byte) + sizeof(int);

    public static Task WriteAsync(Stream stream, DesktopHostCommand command, CancellationToken ct) =>
        WriteFrameAsync(stream, (byte)command.Kind, command.Payload, ct);

    public static Task WriteAsync(Stream stream, DesktopHostEvent hostEvent, CancellationToken ct) =>
        WriteFrameAsync(stream, (byte)hostEvent.Kind, string.Empty, ct);

    public static async Task<DesktopHostCommand?> ReadCommandAsync(Stream stream, CancellationToken ct)
    {
        if (await ReadFrameAsync(stream, ct) is not { } frame)
            return null;

        if (frame.Kind is not ((byte)DesktopHostCommandKind.Navigate or (byte)DesktopHostCommandKind.ShowFailure))
            throw new InvalidDataException($"Unknown desktop host command kind {frame.Kind}.");

        return new DesktopHostCommand((DesktopHostCommandKind)frame.Kind, frame.Payload);
    }

    public static async Task<DesktopHostEvent?> ReadEventAsync(Stream stream, CancellationToken ct)
    {
        if (await ReadFrameAsync(stream, ct) is not { } frame)
            return null;

        if (frame.Kind is not (byte)DesktopHostEventKind.RetryRequested)
            throw new InvalidDataException($"Unknown desktop host event kind {frame.Kind}.");

        return new DesktopHostEvent((DesktopHostEventKind)frame.Kind);
    }

    private static async Task WriteFrameAsync(Stream stream, byte kind, string payload, CancellationToken ct)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var frame = new byte[HeaderLength + payloadBytes.Length];
        frame[0] = kind;
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), payloadBytes.Length);
        payloadBytes.CopyTo(frame.AsSpan(HeaderLength));

        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<(byte Kind, string Payload)?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[HeaderLength];
        if (await stream.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, ct) < HeaderLength)
            return null;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        if (payloadLength < 0)
            throw new InvalidDataException($"Negative desktop host frame payload length {payloadLength}.");

        var payload = new byte[payloadLength];
        if (payloadLength > 0 &&
            await stream.ReadAtLeastAsync(payload, payloadLength, throwOnEndOfStream: false, ct) < payloadLength)
        {
            return null;
        }

        return (header[0], Encoding.UTF8.GetString(payload));
    }
}
