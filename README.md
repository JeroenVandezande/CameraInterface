# CameraInterface

A lightweight .NET library providing a clean abstraction for working with camera devices and image data. It defines common camera properties, supported pixel formats, and efficient serialization/deserialization of image buffers.

---

## Features

- 🎥 **Camera Abstraction** — Provides the `ICamera` interface with common properties (resolution, offsets, ranges, exposure, and image format).
- 🖼 **Image Data Handling** — Encapsulates pixel buffers in the `ImageData` struct with width, height, stride, and format metadata.
- 🔄 **Serialization Support** — Convert images to and from byte arrays or streams for efficient storage and transfer.
- 🧩 **Supported Formats** — Multiple pixel formats such as Mono8, Mono16, Rgb24, Rgb32, Raw8, Raw16.
- ⚡ **Zero-Copy Parsing** — Optimized APIs like `TryFromMemory` avoid unnecessary allocations when possible.
- ⏱ **Units Integration** — Exposure times use [UnitsNet](https://github.com/angularsen/UnitsNet) for type-safe duration handling.

---

## Installation

Add the project or library to your solution. If published as a NuGet package, install via:

```bash
dotnet add package CameraInterface
```

---

## Usage

### Implementing a Camera

```csharp
using CameraInterface;
using UnitsNet;

public class DummyCamera : ICamera
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int XOffset { get; set; } = 0;
    public int YOffset { get; set; } = 0;
    public (int Min, int Max) WidthRange { get; set; } = (320, 1920);
    public (int Min, int Max) HeightRange { get; set; } = (240, 1080);
    public (int Min, int Max) XOffsetRange { get; set; } = (0, 100);
    public (int Min, int Max) YOffsetRange { get; set; } = (0, 100);
    public CameraImageFormat ImageFormat { get; set; } = CameraImageFormat.Rgb24;
    public Duration Exposure { get; set; } = Duration.FromMilliseconds(10);

    public ImageData GetImage()
    {
        // Provide pixel data from the camera here
        var pixels = new byte[Width * Height * 3]; // Rgb24 = 3 bytes per pixel
        return new ImageData(pixels, Width, Height, Width * 3, ImageFormat);
    }
}
```

---

### Working with ImageData

#### Serialize to Array

```csharp
var camera = new DummyCamera();
ImageData img = camera.GetImage();

byte[] serialized = img.ToArray();
```

#### Deserialize from Array

```csharp
ImageData img2 = ImageData.FromArray(serialized);
```

#### Zero-Copy Parse from Memory

```csharp
if (ImageData.TryFromMemory(serialized, out var img3))
{
    Console.WriteLine($"Image parsed: {img3.Width}x{img3.Height}, {img3.Format}");
}
```

#### Read/Write from Stream

```csharp
using var ms = new MemoryStream();
img.WriteTo(ms);

ms.Position = 0;
ImageData img4 = ImageData.ReadFrom(ms);
```

---

## Supported Image Formats

- **Mono8** – 8-bit grayscale  
- **Mono16** – 16-bit grayscale  
- **Rgb24** – 24-bit RGB  
- **Rgb32** – 32-bit RGB (with alpha or padding)  
- **Raw8 / Raw16** – Raw image buffers  

---

## License

MIT License — free to use, modify, and distribute.
