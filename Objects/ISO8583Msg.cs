using System.Collections.Generic;

namespace Qatalyst.Objects;

public class ISO8583Msg(string? messageType = null)
{
    public string MessageType { get; set; } = messageType ?? string.Empty;
    public string Bitmap { get; set; } = string.Empty;

    public string RawMsg { get; set; } = string.Empty;

    public Dictionary<int, ISO8583DataElement> DataElements = [];


    public void AddDataElement(int field, int? length, List<string> value)
    {
        var newDataElement = new ISO8583DataElement(length, value);
        DataElements.Add(field, newDataElement);
    }

    public void ChangeDataElement(int field, int? length, List<string> value)
    {
        if (DataElements.TryGetValue(field, out var iso8583DataElement))
        {
            iso8583DataElement.Length = length;
            iso8583DataElement.Value = value;
        }
        else
        {
            AddDataElement(field, length, value);
        }
    }

    public ISO8583DataElement? GetDataElement(int field)
    {
        DataElements.TryGetValue(field, out var dataElement);
        return dataElement;
    }

    public bool IsRequestMsg() => MessageType.EndsWith("00") || MessageType.EndsWith("20");
}