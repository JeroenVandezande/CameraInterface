using UnitsNet;

namespace CameraInterface;

/// <summary>
/// Represents a configuration interface for camera triggering mechanisms. Classes or records
/// implementing this interface define the behavior of specific trigger modes that the camera
/// can use, such as software-based or no trigger configurations.
/// </summary>
public interface ITriggerConfig
{
    
}

/// <summary>
/// Represents a trigger configuration where no external or software trigger
/// is used. This configuration can be useful for cameras operating in free-run
/// mode or scenarios where no explicit triggering mechanism is required.
/// </summary>
public sealed record NoTrigger()               : ITriggerConfig;

/// <summary>
/// Represents a trigger configuration that uses software-based triggering mechanisms.
/// This configuration enables the camera to be triggered programmatically through
/// software commands, offering precise control over when captures are initiated.
/// see <see cref="ICamera.FireManualTrigger"/>.
/// </summary>
public sealed record SoftwareTrigger()         : ITriggerConfig;

/// <summary>
/// Defines the interface for interacting with a camera. Provides properties for
/// configuring camera settings such as resolution, offsets, exposure time, and
/// obtaining information about configurable ranges. Also includes functionality
/// for triggering captures and receiving image data.
/// </summary>
public interface ICamera
{
    /// <summary>
    /// Gets or sets the width of the camera's resolution in pixels.
    /// This property determines the horizontal size of the image captured by the camera.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the camera's resolution in pixels.
    /// This property determines the vertical size of the image captured by the camera.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the horizontal offset in pixels from the sensor's origin.
    /// This property allows positioning the region of interest horizontally on the camera sensor.
    /// </summary>
    public int XOffset { get; set; }

    /// <summary>
    /// Gets or sets the vertical offset in pixels from the sensor's origin.
    /// This property allows positioning the region of interest vertically on the camera sensor.
    /// </summary>
    public int YOffset { get; set; }

    /// <summary>
    /// Gets the allowable range for the <see cref="Width"/> property.
    /// Returns a tuple containing the minimum and maximum valid width values in pixels.
    /// </summary>
    public (int Min, int Max) WidthRange { get; }

    /// <summary>
    /// Gets the allowable range for the <see cref="Height"/> property.
    /// Returns a tuple containing the minimum and maximum valid height values in pixels.
    /// </summary>
    public (int Min, int Max) HeightRange { get; }

    /// <summary>
    /// Gets the allowable range for the <see cref="XOffset"/> property.
    /// Returns a tuple containing the minimum and maximum valid horizontal offset values in pixels.
    /// </summary>
    public (int Min, int Max) XOffsetRange { get;}

    /// <summary>
    /// Gets the allowable range for the <see cref="YOffset"/> property.
    /// Returns a tuple containing the minimum and maximum valid vertical offset values in pixels.
    /// </summary>
    public (int Min, int Max) YOffsetRange { get;}

    /// <summary>
    /// Gets or sets the exposure time for the camera.
    /// This property controls how long the camera sensor is exposed to light during image capture.
    /// Use UnitsNet.Duration for type-safe time representation (e.g., Duration.FromMilliseconds(10)).
    /// </summary>
    public Duration Exposure { get; set; }

    /// <summary>
    /// Gets or sets the output image format of the camera.
    /// This property defines how the pixel data in the captured images is structured,
    /// such as whether the format is monochrome or RGB with varying bit depths.
    /// </summary>
    public ImageFormat CameraOutputFormat { get; set; }

    /// <summary>
    /// Configures the trigger mechanism for the camera based on the provided
    /// trigger configuration. This method allows the camera to be set up with
    /// a specific triggering mode, such as software or hardware-based triggering.
    /// </summary>
    /// <param name="cfg">The trigger configuration that defines the behavior of the camera's triggering mechanism.</param>
    public void SetupTrigger(ITriggerConfig cfg);

    /// <summary>
    /// Triggers a manual image capture.
    /// This method initiates the camera to capture an image, which will be delivered
    /// through the <see cref="ImageReceived"/> event.
    /// </summary>
    public void FireManualTrigger();

    /// <summary>
    /// Occurs when the camera has captured an image and the image data is available.
    /// Subscribe to this event to receive image data after a capture is triggered.
    /// The event provides an <see cref="ImageData"/> object containing the captured image
    /// along with metadata such as dimensions, format, and pixel data.
    /// </summary>
    public event EventHandler<ImageData> ImageReceived;
}