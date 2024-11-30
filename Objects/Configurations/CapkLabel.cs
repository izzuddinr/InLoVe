namespace Qatalyst.Objects;

public class CapkLabel
{
    public CapkLabel(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; set; }
    public string Label { get; set; }
}