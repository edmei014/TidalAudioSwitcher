using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TidalAudioSwitcher.App.Services.Cdp;

/// <summary>
/// Minimal CDP WebSocket client. Omits the Origin header so Chromium/Electron 148+
/// accepts the handshake without --remote-allow-origins.
/// </summary>
internal sealed class CdpWebSocketClient : IAsyncDisposable
{
    private readonly NetworkStream _stream;
    private readonly TcpClient _tcp;
    private readonly CancellationTokenSource _cts = new();
    private int _nextId;

    private CdpWebSocketClient(TcpClient tcp, NetworkStream stream)
    {
        _tcp = tcp;
        _stream = stream;
    }

    public static async Task<CdpWebSocketClient> ConnectAsync(Uri webSocketUrl, CancellationToken cancellationToken = default)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(webSocketUrl.Host, webSocketUrl.Port, cancellationToken);
        var stream = tcp.GetStream();

        var keyBytes = RandomNumberGenerator.GetBytes(16);
        var secKey = Convert.ToBase64String(keyBytes);
        var path = string.IsNullOrEmpty(webSocketUrl.PathAndQuery) ? "/" : webSocketUrl.PathAndQuery;

        var request = new StringBuilder()
            .Append("GET ").Append(path).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(webSocketUrl.Host).Append(':').Append(webSocketUrl.Port).Append("\r\n")
            .Append("Upgrade: websocket\r\n")
            .Append("Connection: Upgrade\r\n")
            .Append("Sec-WebSocket-Key: ").Append(secKey).Append("\r\n")
            .Append("Sec-WebSocket-Version: 13\r\n")
            .Append("\r\n")
            .ToString();

        var requestBytes = Encoding.ASCII.GetBytes(request);
        await stream.WriteAsync(requestBytes, cancellationToken);

        var response = await ReadHttpHeadersAsync(stream, cancellationToken);
        if (!response.StartsWith("HTTP/1.1 101", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"WebSocket handshake failed: {response.Split('\n')[0].Trim()}");
        }

        return new CdpWebSocketClient(tcp, stream);
    }

    public async Task<JsonElement> SendCommandAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = parameters is null
            ? new { id, method }
            : (object)new { id, method, @params = parameters };

        await SendFrameAsync(JsonSerializer.Serialize(payload), cancellationToken);

        while (true)
        {
            var message = await ReceiveFrameAsync(cancellationToken);
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp) && idProp.GetInt32() == id)
            {
                if (root.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException($"CDP error: {error}");
                }

                return root.TryGetProperty("result", out var result)
                    ? result.Clone()
                    : default;
            }
        }
    }

    public async Task<JsonElement> EvaluateAsync(string expression, bool awaitPromise = false, CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = true,
            awaitPromise
        }, cancellationToken);

        if (result.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        if (result.TryGetProperty("exceptionDetails", out var ex) && ex.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException($"JavaScript exception: {ex}");
        }

        return result.TryGetProperty("result", out var inner) ? inner.Clone() : default;
    }

    private async Task SendFrameAsync(string text, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var maskKey = RandomNumberGenerator.GetBytes(4);

        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] ^= maskKey[i % 4];
        }

        // RFC 6455: client-to-server frames MUST be masked (MASK bit = 1).
        var headerSize = payload.Length < 126 ? 2 : payload.Length <= 0xFFFF ? 4 : 10;
        var frame = new byte[headerSize + 4 + payload.Length];

        frame[0] = 0x81; // FIN + text opcode

        var offset = 2;
        if (payload.Length < 126)
        {
            frame[1] = (byte)(0x80 | payload.Length);
        }
        else if (payload.Length <= 0xFFFF)
        {
            frame[1] = 0x80 | 126;
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), (ushort)payload.Length);
            offset = 4;
        }
        else
        {
            frame[1] = 0x80 | 127;
            BinaryPrimitives.WriteUInt64BigEndian(frame.AsSpan(2, 8), (ulong)payload.Length);
            offset = 10;
        }

        maskKey.CopyTo(frame.AsSpan(offset, 4));
        payload.CopyTo(frame.AsSpan(offset + 4));

        await _stream.WriteAsync(frame, cancellationToken);
    }

    private async Task SendPongAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var maskKey = RandomNumberGenerator.GetBytes(4);
        var masked = (byte[])payload.Clone();
        for (var i = 0; i < masked.Length; i++)
        {
            masked[i] ^= maskKey[i % 4];
        }

        var frame = new byte[2 + 4 + masked.Length];
        frame[0] = 0x8A; // FIN + pong
        frame[1] = (byte)(0x80 | masked.Length);
        maskKey.CopyTo(frame.AsSpan(2, 4));
        masked.CopyTo(frame.AsSpan(6));
        await _stream.WriteAsync(frame, cancellationToken);
    }

    private async Task<string> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        var header = new byte[2];
        await ReadExactAsync(header, cancellationToken);

        var opcode = header[0] & 0x0F;
        var masked = (header[1] & 0x80) != 0;
        var length = header[1] & 0x7F;

        if (length == 126)
        {
            var ext = new byte[2];
            await ReadExactAsync(ext, cancellationToken);
            length = BinaryPrimitives.ReadUInt16BigEndian(ext);
        }
        else if (length == 127)
        {
            var ext = new byte[8];
            await ReadExactAsync(ext, cancellationToken);
            length = (int)BinaryPrimitives.ReadUInt64BigEndian(ext);
        }

        var mask = masked ? new byte[4] : null;
        if (masked)
        {
            await ReadExactAsync(mask!, cancellationToken);
        }

        var payload = new byte[length];
        await ReadExactAsync(payload, cancellationToken);

        if (masked && mask is not null)
        {
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] ^= mask[i % 4];
            }
        }

        if (opcode == 0x8)
        {
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "WebSocket closed by server.");
        }

        if (opcode == 0x9) // ping -> reply pong, then continue
        {
            await SendPongAsync(payload, cancellationToken);
            return await ReceiveFrameAsync(cancellationToken);
        }

        if (opcode == 0xA) // pong
        {
            return await ReceiveFrameAsync(cancellationToken);
        }

        return Encoding.UTF8.GetString(payload);
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("WebSocket connection closed.");
            }

            offset += read;
        }
    }

    private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new byte[1];
        var matched = 0;

        while (true)
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken);
            var ch = (char)buffer[0];
            builder.Append(ch);

            if (matched == 0 && ch == '\r') matched = 1;
            else if (matched == 1 && ch == '\n') matched = 2;
            else if (matched == 2 && ch == '\r') matched = 3;
            else if (matched == 3 && ch == '\n') break;
            else matched = 0;
        }

        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _stream.Dispose();
        _tcp.Dispose();
        _cts.Dispose();
    }
}
