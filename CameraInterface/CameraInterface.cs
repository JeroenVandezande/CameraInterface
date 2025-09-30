using UnitsNet;

namespace CameraInterface;

public interface ICamera
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int XOffset { get; set; }
    public int YOffset { get; set; }
    public (int Min, int Max) WidthRange { get; set; }
    public (int Min, int Max) HeightRange { get; set; }
    public (int Min, int Max) XOffsetRange { get; set; }
    public (int Min, int Max) YOffsetRange { get; set; }
    public CameraImageFormat ImageFormat { get; set; }
    public Duration Exposure { get; set; }
    public ImageData GetImage();
}