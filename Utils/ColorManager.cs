using System;
using System.Collections.Generic;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Qatalyst.Utils;

public static class ColorManager
{
    private static readonly Dictionary<string, SolidColorBrush> ColorCache = new();

    static ColorManager()
    {
        var resourceDictionary = new ResourceDictionary();
        resourceDictionary.Source = new Uri("ms-appx:///Resources/Colors.xaml");

        foreach (var key in resourceDictionary.Keys)
        {
            if (resourceDictionary[key] is SolidColorBrush brush)
            {
                ColorCache[key.ToString() ?? string.Empty] = brush;
            }
        }
    }

    public static SolidColorBrush GetBrush(string key)
    {
        if (ColorCache.TryGetValue(key, out var color))
        {
            return color;
        }

        throw new ArgumentException($"Color with key '{key}' not found in ResourceDictionary.");
    }

    public static Color GetColor(string key)
    {
        if (ColorCache.TryGetValue(key, out var color))
        {
            return color.Color;
        }

        throw new ArgumentException($"Color with key '{key}' not found in ResourceDictionary.");
    }
}