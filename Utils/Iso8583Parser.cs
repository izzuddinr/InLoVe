using System;
using System.Collections.Generic;

namespace InLoVe.Utils;

public static class Iso8583Parser
{
    public static (string MTI, Dictionary<string, string>) ParseIsoMessage(string message)
    {
        var mti = "0000";
        var fields = new Dictionary<string, string>();
        string currentFieldKey = null;
        var currentFieldValue = new List<string>();

        var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Detect MTI in lines with packRequest or unpackResponse
            if (line.Contains("packRequest") || line.Contains("unpackResponse"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    mti = parts[1].Trim();
                }
                continue;
            }

            var fieldParts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
            switch (fieldParts.Length)
            {
                case 2:
                {
                    if (currentFieldKey != null)
                    {
                        fields[currentFieldKey] = string.Join("", currentFieldValue).Trim();
                    }

                    currentFieldKey = fieldParts[0].Trim();
                    currentFieldValue.Clear();
                    currentFieldValue.Add(fieldParts[1].Trim());
                    break;
                }
                case 1 when currentFieldKey != null:
                    currentFieldValue.Add(fieldParts[0].Trim());
                    break;
            }
        }

        // Finalize the last field being processed
        if (currentFieldKey != null)
        {
            fields[currentFieldKey] = string.Join("", currentFieldValue).Trim();
        }

        return (mti, fields);
    }
}