using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using DotRush.Common.Logging;

namespace DotRush.Protocol.JsonRpc;

internal class JsonRpcConnection {
    private const int MaxHeadersSize = 16 * 1024;
    private const int MaxRetainedBufferSize = 1024 * 1024;

    private readonly PipeReader reader;
    private readonly Stream output;
    private readonly object writeLock;
    private readonly Utf8JsonWriter writer;
    private ArrayBufferWriter<byte> writeBuffer;

    public JsonRpcConnection(Stream input, Stream output) {
        this.reader = PipeReader.Create(input);
        this.output = output;
        this.writeLock = new object();
        this.writeBuffer = new ArrayBufferWriter<byte>();
        this.writer = new Utf8JsonWriter(writeBuffer, new JsonWriterOptions { Encoder = ProtocolSerializer.Options.Encoder });
    }

    // Returns null when the input stream is closed. Throws JsonException when the content is not a valid
    // message (the stream stays in sync) and InvalidDataException when the framing itself is broken.
    public async ValueTask<JsonRpcMessage?> ReadAsync(CancellationToken cancellationToken) {
        var contentLength = await ReadHeadersAsync(cancellationToken).ConfigureAwait(false);
        if (contentLength < 0)
            return null;

        var result = await reader.ReadAtLeastAsync(contentLength, cancellationToken).ConfigureAwait(false);
        if (result.Buffer.Length < contentLength) {
            reader.AdvanceTo(result.Buffer.End);
            return null;
        }

        var content = result.Buffer.Slice(0, contentLength);
        try {
            return Deserialize(content) ?? throw new JsonException("Message is empty");
        } finally {
            reader.AdvanceTo(content.End);
        }
    }

    public void SendRequest(long id, string method, object? parameters) {
        Send(new RequestId(id), method, parameters, null);
    }
    public void SendNotification(string method, object? parameters) {
        Send(null, method, parameters, null);
    }
    public void SendResult(RequestId id, object? result) {
        Send(id, null, result, null);
    }
    public void SendError(RequestId id, int code, string message) {
        Send(id, null, null, new ResponseError { Code = code, Message = message });
    }

    private async ValueTask<int> ReadHeadersAsync(CancellationToken cancellationToken) {
        while (true) {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            if (TryParseHeaders(buffer, out var contentLength, out var consumed)) {
                reader.AdvanceTo(consumed);
                return contentLength;
            }
            if (buffer.Length > MaxHeadersSize)
                throw new InvalidDataException("Message headers are too long");

            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted)
                return -1;
        }
    }
    private void Send(RequestId? id, string? method, object? content, ResponseError? error) {
        lock (writeLock) {
            if (writeBuffer.Capacity > MaxRetainedBufferSize)
                writeBuffer = new ArrayBufferWriter<byte>();

            writeBuffer.ResetWrittenCount();
            writer.Reset(writeBuffer);
            writer.WriteStartObject();
            writer.WriteString("jsonrpc"u8, "2.0"u8);
            if (id != null) {
                writer.WritePropertyName("id"u8);
                JsonSerializer.Serialize(writer, id.Value, ProtocolSerializer.Options);
            }
            if (method != null) {
                writer.WriteString("method"u8, method);
                if (content != null) {
                    writer.WritePropertyName("params"u8);
                    JsonSerializer.Serialize(writer, content, content.GetType(), ProtocolSerializer.Options);
                }
            }
            else if (error != null) {
                writer.WritePropertyName("error"u8);
                JsonSerializer.Serialize(writer, error, ProtocolSerializer.Options);
            }
            else {
                // A response without an error must have the result member, even if it is null
                writer.WritePropertyName("result"u8);
                if (content != null)
                    JsonSerializer.Serialize(writer, content, content.GetType(), ProtocolSerializer.Options);
                else
                    writer.WriteNullValue();
            }
            writer.WriteEndObject();
            writer.Flush();

            Span<byte> header = stackalloc byte[32];
            "Content-Length: "u8.CopyTo(header);
            Utf8Formatter.TryFormat(writeBuffer.WrittenCount, header.Slice(16), out var digits);
            "\r\n\r\n"u8.CopyTo(header.Slice(16 + digits));

            try {
                output.Write(header.Slice(0, 16 + digits + 4));
                output.Write(writeBuffer.WrittenSpan);
                output.Flush();
            } catch (Exception e) when (e is IOException || e is ObjectDisposedException) {
                CurrentSessionLogger.Debug($"Failed to write a message, the output stream is closed: {e.Message}");
            }
        }
    }

    private static bool TryParseHeaders(ReadOnlySequence<byte> buffer, out int contentLength, out SequencePosition consumed) {
        var sequenceReader = new SequenceReader<byte>(buffer);
        contentLength = -1;
        consumed = default;

        while (sequenceReader.TryReadTo(out ReadOnlySpan<byte> line, "\r\n"u8)) {
            if (line.IsEmpty) {
                if (contentLength < 0)
                    throw new InvalidDataException("Content-Length header is missing");

                consumed = sequenceReader.Position;
                return true;
            }

            var separator = line.IndexOf((byte)':');
            if (separator <= 0 || !Ascii.EqualsIgnoreCase(line.Slice(0, separator), "Content-Length"u8))
                continue;

            var value = line.Slice(separator + 1).Trim((byte)' ');
            if (!Utf8Parser.TryParse(value, out contentLength, out var bytesConsumed) || bytesConsumed != value.Length || contentLength < 0)
                throw new InvalidDataException($"Invalid Content-Length header: '{Encoding.ASCII.GetString(value)}'");
        }

        return false;
    }
    private static JsonRpcMessage? Deserialize(ReadOnlySequence<byte> content) {
        var jsonReader = new Utf8JsonReader(content);
        return JsonSerializer.Deserialize<JsonRpcMessage>(ref jsonReader, ProtocolSerializer.Options);
    }
}
