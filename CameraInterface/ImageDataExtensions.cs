namespace CameraInterface;

public static class ImageDataExtensions
{
    /// <summary>Horizontal concatenation (side-by-side). Requires the same height & format.</summary>
    public static ImageData ConcatHorizontal(this IReadOnlyList<ImageData> images)
    {
        if (images is null || images.Count == 0) throw new ArgumentException("No images.");
        var fmt = images[0].Format;
        int h = images[0].Height;
        int bpp = images[0].BytesPerPixel;

        int wSum = 0;
        foreach (var im in images)
        {
            if (im.Format != fmt) throw new ArgumentException("All images must have same format.");
            if (im.Height != h)   throw new ArgumentException("All images must have same height.");
            wSum += im.Width;
        }

        int dstW = wSum;
        int dstH = h;
        int dstStride = dstW * bpp;
        var dst = new byte[dstStride * dstH];

        int xOffsetPx = 0;
        foreach (var im in images)
        {
            var src = im.Pixels.Span;
            int copyBytesPerRow = im.Width * bpp;
            for (int y = 0; y < dstH; y++)
            {
                int srcRow = y * im.Stride;
                int dstRow = y * dstStride + xOffsetPx * bpp;
                src.Slice(srcRow, copyBytesPerRow).CopyTo(dst.AsSpan(dstRow, copyBytesPerRow));
            }
            xOffsetPx += im.Width;
        }

        return new ImageData(dst, dstW, dstH, dstStride, fmt);
    }

    /// <summary>Vertical concatenation (stack). Requires the same width & format.</summary>
    public static ImageData ConcatVertical(this IReadOnlyList<ImageData> images)
    {
        if (images is null || images.Count == 0) throw new ArgumentException("No images.");
        var fmt = images[0].Format;
        int w = images[0].Width;
        int bpp = images[0].BytesPerPixel;

        int hSum = 0;
        foreach (var im in images)
        {
            if (im.Format != fmt) throw new ArgumentException("All images must have same format.");
            if (im.Width  != w)   throw new ArgumentException("All images must have same width.");
            hSum += im.Height;
        }

        int dstW = w;
        int dstH = hSum;
        int dstStride = dstW * bpp;
        var dst = new byte[dstStride * dstH];

        int yOffset = 0;
        foreach (var im in images)
        {
            var src = im.Pixels.Span;
            int copyBytesPerRow = dstW * bpp;
            for (int y = 0; y < im.Height; y++)
            {
                int srcRow = y * im.Stride;
                int dstRow = (yOffset + y) * dstStride;
                src.Slice(srcRow, copyBytesPerRow).CopyTo(dst.AsSpan(dstRow, copyBytesPerRow));
            }
            yOffset += im.Height;
        }

        return new ImageData(dst, dstW, dstH, dstStride, fmt);
    }
}