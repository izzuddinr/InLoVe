using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;

namespace Qatalyst.Utils;

public static class ColorManager
{
    private static readonly Dictionary<string, string> ColorCache = new();
    private const string DefaultHexColorBlack = "#000000";
    private const string DefaultHexColorWhite = "#FFFFFF";

    static ColorManager()
    {
        LoadColorsFromJson("colors.json");
    }

    public static SolidColorBrush GetBrush(string nameOrHexColor)
    {
        try
        {
            // Check if the name exists in the ColorCache
            return CreateBrushFromHexCode(ColorCache.GetValueOrDefault(nameOrHexColor, DefaultHexColorWhite));

            // If not found, assume it's a hex code and create the brush dynamically
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Could not create SolidColorBrush for '{nameOrHexColor}'.", e);
        }
    }

    public static Color GetColor(string nameOrHexColor)
    {
        try
        {
            // Check if the name exists in the ColorCache
            return ColorCache.GetValueOrDefault(nameOrHexColor, DefaultHexColorWhite).ToColor();

            // If not found, assume it's a hex code and create the color dynamically
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Could not create color for '{nameOrHexColor}'.", e);
        }
    }

    private static SolidColorBrush CreateBrushFromHexCode(string hexColor)
    {
        var color = hexColor.ToColor();
        return new SolidColorBrush(color);
    }

    private static void LoadColorsFromJson(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");
        }

        var jsonContent = File.ReadAllText(jsonFilePath);

        try
        {
            // Deserialize JSON into dictionary
            var colorDefinitions = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

            if (colorDefinitions == null)
            {
                throw new InvalidOperationException("Invalid JSON format.");
            }

            // Populate ColorCache with hex codes
            foreach (var kvp in colorDefinitions)
            {
                ColorCache[kvp.Key] = kvp.Value;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to load colors from JSON.", ex);
        }
    }


    /// <summary>
    /// Creates a <see cref="Color"/> from a XAML color string.
    /// Any format used in XAML should work.
    /// </summary>
    /// <param name="colorString">The XAML color string.</param>
    /// <returns>The created <see cref="Color"/>.</returns>
    public static Color ToColor(this string colorString)
    {
        if (string.IsNullOrEmpty(colorString))
        {
            ThrowArgumentException();
        }

        if (colorString[0] == '#')
        {
            switch (colorString.Length)
            {
                case 9:
                {
                    var cuint = Convert.ToUInt32(colorString.Substring(1), 16);
                    var a = (byte)(cuint >> 24);
                    var r = (byte)((cuint >> 16) & 0xff);
                    var g = (byte)((cuint >> 8) & 0xff);
                    var b = (byte)(cuint & 0xff);

                    return Color.FromArgb(a, r, g, b);
                }

                case 7:
                {
                    var cuint = Convert.ToUInt32(colorString.Substring(1), 16);
                    var r = (byte)((cuint >> 16) & 0xff);
                    var g = (byte)((cuint >> 8) & 0xff);
                    var b = (byte)(cuint & 0xff);

                    return Color.FromArgb(255, r, g, b);
                }

                case 5:
                {
                    var cuint = Convert.ToUInt16(colorString.Substring(1), 16);
                    var a = (byte)(cuint >> 12);
                    var r = (byte)((cuint >> 8) & 0xf);
                    var g = (byte)((cuint >> 4) & 0xf);
                    var b = (byte)(cuint & 0xf);
                    a = (byte)(a << 4 | a);
                    r = (byte)(r << 4 | r);
                    g = (byte)(g << 4 | g);
                    b = (byte)(b << 4 | b);

                    return Color.FromArgb(a, r, g, b);
                }

                case 4:
                {
                    var cuint = Convert.ToUInt16(colorString.Substring(1), 16);
                    var r = (byte)((cuint >> 8) & 0xf);
                    var g = (byte)((cuint >> 4) & 0xf);
                    var b = (byte)(cuint & 0xf);
                    r = (byte)(r << 4 | r);
                    g = (byte)(g << 4 | g);
                    b = (byte)(b << 4 | b);

                    return Color.FromArgb(255, r, g, b);
                }

                default: return ThrowFormatException();
            }
        }

        if (colorString.Length > 3 && colorString[0] == 's' && colorString[1] == 'c' && colorString[2] == '#')
        {
            var values = colorString.Split(',');

            if (values.Length == 4)
            {
                var scA = double.Parse(values[0].Substring(3), CultureInfo.InvariantCulture);
                var scR = double.Parse(values[1], CultureInfo.InvariantCulture);
                var scG = double.Parse(values[2], CultureInfo.InvariantCulture);
                var scB = double.Parse(values[3], CultureInfo.InvariantCulture);

                return Color.FromArgb((byte)(scA * 255), (byte)(scR * 255), (byte)(scG * 255), (byte)(scB * 255));
            }

            if (values.Length == 3)
            {
                var scR = double.Parse(values[0].Substring(3), CultureInfo.InvariantCulture);
                var scG = double.Parse(values[1], CultureInfo.InvariantCulture);
                var scB = double.Parse(values[2], CultureInfo.InvariantCulture);

                return Color.FromArgb(255, (byte)(scR * 255), (byte)(scG * 255), (byte)(scB * 255));
            }

            return ThrowFormatException();
        }

        var prop = typeof(Colors).GetTypeInfo().GetDeclaredProperty(colorString);

        if (prop != null)
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            return (Color)prop.GetValue(null);
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }

        return ThrowFormatException();

        static void ThrowArgumentException() => throw new ArgumentException("The parameter \"colorString\" must not be null or empty.");
        static Color ThrowFormatException() => throw new FormatException("The parameter \"colorString\" is not a recognized Color format.");
    }

    public static string ToHex(this Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static int ToInt(this Color color)
    {
        var a = color.A + 1;
        var col = (color.A << 24) | ((byte)((color.R * a) >> 8) << 16) | ((byte)((color.G * a) >> 8) << 8) | (byte)((color.B * a) >> 8);
        return col;
    }

}