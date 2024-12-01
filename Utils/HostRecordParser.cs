using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Qatalyst.Utils;

public static class HostRecordParser
{
    public static JObject? ParseHostRecord(string inputString)
    {
        var parts = inputString.Split(" = ", 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2) return null;

        var jsonObject = ConvertStringToJson(parts[1]);

        return jsonObject;
    }

    public static JObject? ParseHostRecord(List<string> hostRecord)
    {
        var inputString = string.Join("", hostRecord);
        return ParseHostRecord(inputString);
    }

    static JObject? ConvertStringToJson(string json)
    {
        try
        {
            JObject parsedJson = JObject.Parse(json);
            return parsedJson;
        }
        catch (JsonReaderException ex)
        {
            Console.WriteLine($"Invalid JSON: {ex.Message}");
            return null;
        }
    }

    static void PrintJsonObjectToConsole(JObject? jsonObject)
    {
        Console.WriteLine("Formatted JSON Object:");
        Console.WriteLine(jsonObject.ToString(Formatting.Indented));
    }
}