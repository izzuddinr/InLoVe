using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Color = Windows.UI.Color;

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
            return CreateColorFromHexCode(ColorCache.GetValueOrDefault(nameOrHexColor, DefaultHexColorWhite));

            // If not found, assume it's a hex code and create the color dynamically
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Could not create color for '{nameOrHexColor}'.", e);
        }
    }

    private static SolidColorBrush CreateBrushFromHexCode(string hexColor)
    {
        var color = CreateColorFromHexCode(hexColor);
        return new SolidColorBrush(color);
    }

    private static Color CreateColorFromHexCode(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            throw new ArgumentException("Hex color code cannot be null or empty.", nameof(hexColor));
        }

        if (!hexColor.StartsWith("#"))
        {
            hexColor = "#" + hexColor;
        }

        if (hexColor.Length != 7 && hexColor.Length != 9)
        {
            throw new ArgumentException($"Invalid hex color code length: {hexColor}. Expected #RRGGBB or #AARRGGBB format.");
        }

        // Parse ARGB values
        byte a = 255; // Default alpha value (opaque)
        var startIndex = 1;

        if (hexColor.Length == 9) // #AARRGGBB format
        {
            a = Convert.ToByte(hexColor.Substring(startIndex, 2), 16);
            startIndex += 2;
        }

        var r = Convert.ToByte(hexColor.Substring(startIndex, 2), 16);
        var g = Convert.ToByte(hexColor.Substring(startIndex + 2, 2), 16);
        var b = Convert.ToByte(hexColor.Substring(startIndex + 4, 2), 16);

        return Color.FromArgb(a, r, g, b);
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
}