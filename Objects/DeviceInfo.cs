using System;

namespace Qatalyst.Objects;

public class DeviceInfo
{
    public string SerialNumber { get; set; }
    public string Manufacturer  { get; set; }
    public string Model  { get; set; }
    public string ImageLocation { get; set; }

    public override string ToString()
    {
        return $"SerialNumber: {SerialNumber}{Environment.NewLine}" +
               $"Manufacturer: {Manufacturer}{Environment.NewLine}" +
               $"Model: {Model}{Environment.NewLine}" +
               $"ImageLocation: {ImageLocation}{Environment.NewLine}";
    }
}
