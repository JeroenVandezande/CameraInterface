using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace CameraInterface;

public readonly record struct ImageData(
    ReadOnlyMemory<byte> Pixels,
    int Width,
    int Height,
    int Stride,
    CameraImageFormat Format)
{
    public const int Version = 1;
    private const uint Magic = 0x42474D49; // "IMGB" little-endian
    private const int HeaderSize = 28;     // bytes before pixel payload

    public int BytesPerPixel => Format switch
    {
        CameraImageFormat.Rgb32 => 4,
        CameraImageFormat.Mono8 => 1,
        CameraImageFormat.Raw8 => 1,
        CameraImageFormat.Mono16 => 2,
        CameraImageFormat.Raw16 => 2,
        CameraImageFormat.Rgb24 => 3,
        _ => throw new NotSupportedException()
    };

    // ----------- Serialize to a new byte[] -----------
    public byte[] ToArray()
    {
        var dataSize = Pixels.Length;
        checked
        {
            var buf = new byte[HeaderSize + dataSize];
            WriteHeader(buf, dataSize);
            if (dataSize > 0)
                Pixels.Span.CopyTo(buf.AsSpan(HeaderSize, dataSize));
            return buf;
        }
    }

    // ----------- Serialize to any Stream (no big temp array) -----------
    public void WriteTo(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        Span<byte> header = stackalloc byte[HeaderSize];
        WriteHeader(header, Pixels.Length);
        stream.Write(header);
        if (Pixels.Length > 0)
        {
            if (MemoryMarshal.TryGetArray(Pixels, out var seg) && seg.Offset == 0 && seg.Count == Pixels.Length)
            {
                stream.Write(seg.Array!, 0, seg.Count);
            }
            else
            {
                // Stream.Write(ReadOnlySpan<byte>) exists on .NET 8+
                stream.Write(Pixels.Span);
            }
        }
    }

    // ----------- Parse from ReadOnlySpan<byte> (copies pixels) -----------
    public static ImageData FromArray(ReadOnlySpan<byte> buffer)
    {
        ParseHeader(buffer, out int width, out int height, out int stride, out CameraImageFormat fmt, out int size);
        var pixels = size == 0 ? ReadOnlyMemory<byte>.Empty : buffer.Slice(HeaderSize, size).ToArray();
        return new ImageData(pixels, width, height, stride, fmt);
    }

    // ----------- Zero-copy parse when data is array-backed -----------
    public static bool TryFromMemory(ReadOnlyMemory<byte> memory, out ImageData image)
    {
        var span = memory.Span;
        try
        {
            ParseHeader(span, out int width, out int height, out int stride, out CameraImageFormat fmt, out int size);
            // Slice WITHOUT copying pixels; lifetime tied to the original memory
            var pixMem = size == 0 ? ReadOnlyMemory<byte>.Empty : memory.Slice(HeaderSize, size);
            image = new ImageData(pixMem, width, height, stride, fmt);
            return true;
        }
        catch
        {
            image = default;
            return false;
        }
    }

    // ----------- Parse from a Stream (allocates exactly once for pixels) -----------
    public static ImageData ReadFrom(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        Span<byte> header = stackalloc byte[HeaderSize];
        FillExactly(stream, header);

        ParseHeader(header, out int width, out int height, out int stride, out CameraImageFormat fmt, out int size);

        byte[] pixels = size == 0 ? Array.Empty<byte>() : new byte[size];
        if (size > 0) FillExactly(stream, pixels.AsSpan());
        return new ImageData(pixels, width, height, stride, fmt);
    }

    // ===== Internals =====
    private void WriteHeader(Span<byte> span, int dataSize)
    {
        if (span.Length < HeaderSize) throw new ArgumentException("Header span too small.");
        if (Width <= 0 || Height <= 0 || Stride <= 0) throw new InvalidOperationException("Invalid dimensions/stride.");
        if (!Enum.IsDefined(typeof(CameraImageFormat), Format)) throw new InvalidOperationException("Unknown PixelFormat.");
        if (dataSize < 0) throw new InvalidOperationException("Negative data size.");

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), Magic);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(4, 4), Version);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(8, 4), Width);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(12, 4), Height);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(16, 4), Stride);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(20, 4), (int)Format);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(24, 4), dataSize);
    }

    private static void ParseHeader(ReadOnlySpan<byte> data,
        out int width, out int height, out int stride, out CameraImageFormat format, out int dataSize)
    {
        if (data.Length < HeaderSize) throw new ArgumentException("Buffer too small (needs ≥ 28 bytes).");

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
        if (magic != Magic) throw new ArgumentException("Bad magic (not IMGB).");

        int version = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
        if (version != Version)
            throw new NotSupportedException($"Unsupported ImageBuffer version {version}.");

        width    = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        height   = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
        stride   = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16, 4));
        var fmtI = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20, 4));
        dataSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(24, 4));

        if (width <= 0 || height <= 0 || stride <= 0) throw new ArgumentException("Invalid dimensions/stride.");
        if (dataSize < 0 || HeaderSize + dataSize > data.Length) throw new ArgumentException("Invalid pixel data size.");
        format = (CameraImageFormat)fmtI;
        if (!Enum.IsDefined(typeof(CameraImageFormat), format)) throw new ArgumentException("Unknown PixelFormat.");
    }

    private static void FillExactly(Stream s, Span<byte> target)
    {
        int readTotal = 0;
        while (readTotal < target.Length)
        {
            int n = s.Read(target.Slice(readTotal));
            if (n <= 0) throw new EndOfStreamException("Unexpected EOF.");
            readTotal += n;
        }
    }
}