using System;

namespace CameraInterface
{
    /// <summary>
    /// Specifies the pixel format for image data.
    /// Defines how pixel data is organized and interpreted in the image buffer.
    /// </summary>
    public enum ImageFormat
    {
        /// <summary>
        /// 8-bit monochrome (grayscale) format.
        /// Each pixel is represented by a single byte with values from 0 (black) to 255 (white).
        /// Uses 1 byte per pixel.
        /// </summary>
        Mono8,

        /// <summary>
        /// 16-bit monochrome (grayscale) format.
        /// Each pixel is represented by a 16-bit value, providing greater dynamic range than Mono8.
        /// Uses 2 bytes per pixel.
        /// </summary>
        Mono16,

        /// <summary>
        /// 32-bit monochrome (grayscale) format.
        /// Each pixel is represented by a 32-bit value, providing maximum grayscale precision.
        /// Uses 4 bytes per pixel.
        /// </summary>
        Mono32,

        /// <summary>
        /// 24-bit RGB color format.
        /// Each pixel consists of three 8-bit channels: Red, Green, and Blue (in that order).
        /// Uses 3 bytes per pixel.
        /// </summary>
        Rgb24,

        /// <summary>
        /// 32-bit RGB color format.
        /// Each pixel consists of four 8-bit channels, typically Red, Green, Blue, and Alpha (or padding).
        /// Uses 4 bytes per pixel.
        /// </summary>
        Rgb32
    }
    
}
