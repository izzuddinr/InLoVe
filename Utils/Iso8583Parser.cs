using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Qatalyst.Objects;

namespace Qatalyst.Utils;

public static class Iso8583Parser
{
    private static readonly List<int> asciiFields =
    [
        37, // an 12	Retrieval reference number
        38, // an 6	Authorization identification response
        39, // an 2	Response code
        40, // an 3	Service restriction code
        41, // ans 8	Card acceptor terminal identification
        42, // ans 15	Card acceptor identification code
        43, // ans 40	Card acceptor name/location
        44, // an ..25	Additional response data
        54, // an ...120 Additional amounts
        60, // an ...120 Additional amounts
        61, // an ...120 Additional amounts
        62, // an ...120 Additional amounts
        63, // an ...120 Additional amounts
        91, // an 1	File update code
        92, // an 2	File security code
        93, // an 5	Response indicator
        94, // an 7	Service indicator
        95, // an 42 Replacement amounts
        98, // ans 25 Payee
        101, // ans ..17 File name
        102, // ans ..28 Account identification 1
        103, // ans ..28 Account identification 2
        104  // ans ...100 Transaction description
    ];

    public static ISO8583Msg ParseIsoMessage(string message)
    {
        var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var isoMsg = new ISO8583Msg();
        var currentFieldNumber = 0;

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (line.Contains("packRequest") || line.Contains("unpackResponse"))
            {
                if (parts.Length > 1) isoMsg.MessageType = parts[1].Trim();
                continue;
            }

            if (currentFieldNumber == 0)
            {
                isoMsg.Bitmap = parts[0].Trim();
                currentFieldNumber = 1;
                continue;
            }

            switch (parts.Length)
            {
                case 2:
                {
                    currentFieldNumber = int.Parse(parts[0].Trim());
                    isoMsg.ChangeDataElement(currentFieldNumber, null, [parts[1].Trim()]);
                    break;
                }
                case 1:
                {
                    var currentDataElement = isoMsg.GetDataElement(currentFieldNumber);
                    if (currentDataElement?.Value == null) break;

                    if (currentDataElement.Length == null)
                    {
                        isoMsg.ChangeDataElement(currentFieldNumber,int.Parse(currentDataElement.Value[0]), [parts[0].Trim()]);
                    }
                    else
                    {
                        currentDataElement.Value.Add(parts[0].Trim());
                        isoMsg.ChangeDataElement(currentFieldNumber, currentDataElement.Length, currentDataElement.Value);
                    }
                    break;
                }
            }
        }

        FormatIsoDataElement(isoMsg);
        return isoMsg;
    }

    public static void FormatIsoDataElement(ISO8583Msg isoMsg)
    {
        foreach (var data in isoMsg.DataElements)
        {
            var length = data.Value.Length;
            var values = data.Value.Value;

            if (!asciiFields.Contains(data.Key)) continue;

            isoMsg.ChangeDataElement(data.Key, length, values.Select(HexToString).ToList());
        }
    }

    public static string HexToString(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentException("Input hex string cannot be null or empty.");

        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string length must be even.");

        var result = new StringBuilder(hex.Length / 2);
        for (var i = 0; i < hex.Length; i += 2)
        {
            // Convert each pair of hex digits to a byte
            var hexPair = hex.Substring(i, 2);
            var byteValue = Convert.ToByte(hexPair, 16);

            // Append the ASCII character representation of the byte
            result.Append((char)byteValue);
        }
        return result.ToString();
    }
}