using System.IO;

namespace Qatalyst.Objects;

public static class DeviceInfoExtensions
{

    private const string DefaultImageLocationIngenico = "ms-appx:///Assets/Devices/DX8000.png";
    private const string DefaultImageLocationOther = "ms-appx:///Assets/Devices/DEFAULT.png";
    public static string GetImageLocation(this (string manufacturer, string model) device)
    {
        var imagePath = $"ms-appx:///Assets/Devices/{device.model}.png";

        return File.Exists(imagePath)
            ? imagePath
            : device.manufacturer.Contains("ingenico", System.StringComparison.CurrentCultureIgnoreCase)
            ? DefaultImageLocationIngenico
            : DefaultImageLocationOther;
    }
}