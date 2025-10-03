using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace CameraInterface;

/// <summary>
/// Represents image data with pixel buffer, dimensions, stride, and format information.
/// Provides efficient serialization and deserialization capabilities for transferring
/// image data to and from byte arrays or streams. Supports both copy-based and zero-copy parsing.
/// </summary>
/// <param name="Pixels">The read-only memory buffer containing the raw pixel data.</param>
/// <param name="Width">The width of the image in pixels.</param>
/// <param name="Height">The height of the image in pixels.</param>
/// <param name="Stride">The number of bytes per row in the pixel buffer, including any padding.</param>
/// <param name="Format">The pixel format of the image data.</param>
public readonly record struct ImageData(
    ReadOnlyMemory<byte> Pixels,
    int Width,
    int Height,
    int Stride,
    ImageFormat Format)
{
    /// <summary>
    /// The version number of the ImageData serialization format.
    /// Used to ensure compatibility when deserializing image data.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Magic number identifier for serialized image data: "IMGB" in little-endian format (0x42474D49).
    /// Used to validate that a byte buffer contains valid ImageData.
    /// </summary>
    private const uint Magic = 0x42474D49;

    /// <summary>
    /// The size in bytes of the serialization header that precedes the pixel payload.
    /// </summary>
    private const int HeaderSize = 28;    

    /// <summary>
    /// Gets the number of bytes used per pixel based on the current <see cref="Format"/>.
    /// </summary>
    /// <returns>
    /// The bytes per pixel: 1 for Mono8, 2 for Mono16, 3 for Rgb24, 4 for Mono32 and Rgb32.
    /// </returns>
    /// <exception cref="NotSupportedException">Thrown if the format is not supported.</exception>
    public int BytesPerPixel => Format switch
    {
        ImageFormat.Rgb32 => 4,
        ImageFormat.Mono8 => 1,
        ImageFormat.Mono16 => 2,
        ImageFormat.Mono32 => 4,
        ImageFormat.Rgb24 => 3,
        _ => throw new NotSupportedException()
    };

    /// <summary>
    /// Serializes the image data to a new byte array.
    /// The array contains a header followed by the pixel data.
    /// </summary>
    /// <returns>A byte array containing the complete serialized image data.</returns>
    /// <exception cref="OverflowException">Thrown if the total size exceeds array limits.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the image has invalid dimensions, stride, or format.</exception>
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

    /// <summary>
    /// Serializes the image data directly to a stream without allocating a large temporary array.
    /// Writes the header followed by the pixel data.
    /// </summary>
    /// <param name="stream">The stream to write the image data to.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the image has invalid dimensions, stride, or format.</exception>
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

    /// <summary>
    /// Deserializes image data from a byte buffer, copying the pixel data to a new array.
    /// </summary>
    /// <param name="buffer">The buffer containing serialized image data with header and pixels.</param>
    /// <returns>A new <see cref="ImageData"/> instance with copied pixel data.</returns>
    /// <exception cref="ArgumentException">Thrown if the buffer is too small, has an invalid magic number, invalid dimensions, or invalid data size.</exception>
    /// <exception cref="NotSupportedException">Thrown if the version or format is not supported.</exception>
    public static ImageData FromArray(ReadOnlySpan<byte> buffer)
    {
        ParseHeader(buffer, out int width, out int height, out int stride, out ImageFormat fmt, out int size);
        var pixels = size == 0 ? ReadOnlyMemory<byte>.Empty : buffer.Slice(HeaderSize, size).ToArray();
        return new ImageData(pixels, width, height, stride, fmt);
    }

    /// <summary>
    /// Attempts to deserialize image data from a memory buffer without copying pixel data (zero-copy).
    /// The returned <see cref="ImageData"/> shares the same underlying memory as the input.
    /// </summary>
    /// <param name="memory">The memory buffer containing serialized image data.</param>
    /// <param name="image">When successful, contains the deserialized image data with a reference to the original memory.</param>
    /// <returns><c>true</c> if parsing was successful; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// The lifetime of the returned image data is tied to the input memory buffer.
    /// Ensure the input memory remains valid while using the image data.
    /// </remarks>
    public static bool TryFromMemory(ReadOnlyMemory<byte> memory, out ImageData image)
    {
        var span = memory.Span;
        try
        {
            ParseHeader(span, out int width, out int height, out int stride, out ImageFormat fmt, out int size);
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

    /// <summary>
    /// Deserializes image data from a stream, allocating exactly once for the pixel buffer.
    /// Reads the header first, then allocates and reads the pixel data.
    /// </summary>
    /// <param name="stream">The stream to read image data from.</param>
    /// <returns>A new <see cref="ImageData"/> instance with pixel data read from the stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the header or pixel data is invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown if the version or format is not supported.</exception>
    /// <exception cref="EndOfStreamException">Thrown if the stream ends unexpectedly before all data is read.</exception>
    public static ImageData ReadFrom(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        Span<byte> header = stackalloc byte[HeaderSize];
        FillExactly(stream, header);

        ParseHeader(header, out int width, out int height, out int stride, out ImageFormat fmt, out int size);

        byte[] pixels = size == 0 ? Array.Empty<byte>() : new byte[size];
        if (size > 0) FillExactly(stream, pixels.AsSpan());
        return new ImageData(pixels, width, height, stride, fmt);
    }

    // ===== Internals =====
    
    /// <summary>
    /// Writes the image metadata header to a span of bytes.
    /// </summary>
    /// <param name="span">The span to write the header to (must be at least <see cref="HeaderSize"/> bytes).</param>
    /// <param name="dataSize">The size of the pixel data in bytes.</param>
    /// <exception cref="ArgumentException">Thrown if the span is too small.</exception>
    /// <exception cref="InvalidOperationException">Thrown if dimensions, stride, format, or data size are invalid.</exception>
    private void WriteHeader(Span<byte> span, int dataSize)
    {
        if (span.Length < HeaderSize) throw new ArgumentException("Header span too small.");
        if (Width <= 0 || Height <= 0 || Stride <= 0) throw new InvalidOperationException("Invalid dimensions/stride.");
        if (!Enum.IsDefined(typeof(ImageFormat), Format)) throw new InvalidOperationException("Unknown PixelFormat.");
        if (dataSize < 0) throw new InvalidOperationException("Negative data size.");

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), Magic);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(4, 4), Version);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(8, 4), Width);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(12, 4), Height);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(16, 4), Stride);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(20, 4), (int)Format);
        BinaryPrimitives.WriteInt32LittleEndian (span.Slice(24, 4), dataSize);
    }

    /// <summary>
    /// Parses the image metadata header from a byte buffer.
    /// </summary>
    /// <param name="data">The buffer containing the header data.</param>
    /// <param name="width">The parsed image width.</param>
    /// <param name="height">The parsed image height.</param>
    /// <param name="stride">The parsed row stride.</param>
    /// <param name="format">The parsed pixel format.</param>
    /// <param name="dataSize">The parsed pixel data size.</param>
    /// <exception cref="ArgumentException">Thrown if the buffer is too small, has invalid magic number, dimensions, or data size.</exception>
    /// <exception cref="NotSupportedException">Thrown if the version is not supported.</exception>
    private static void ParseHeader(ReadOnlySpan<byte> data,
        out int width, out int height, out int stride, out ImageFormat format, out int dataSize)
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
        format = (ImageFormat)fmtI;
        if (!Enum.IsDefined(typeof(ImageFormat), format)) throw new ArgumentException("Unknown PixelFormat.");
    }

    /// <summary>
    /// Reads from a stream until the target span is completely filled.
    /// </summary>
    /// <param name="s">The stream to read from.</param>
    /// <param name="target">The span to fill with data from the stream.</param>
    /// <exception cref="EndOfStreamException">Thrown if the stream ends before the target span is filled.</exception>
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