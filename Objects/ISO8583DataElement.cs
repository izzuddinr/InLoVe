using System.Collections.Generic;
using System.Linq;

namespace InLoVe.Objects;

public class ISO8583DataElement(int? length, List<string> value)
{
    public int? Length { get; set; } = length;
    public List<string> Value { get; set; } = value;

    public int GetValueLength()
    {
        return Value.Sum(value => value.Length);
    }
}