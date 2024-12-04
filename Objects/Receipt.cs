using System.Collections.Generic;

namespace Qatalyst.Objects;

public class Receipt
{
    public LinePrintData LinePrintData { get; set; }
}


public class LinePrintData
{
    public List<Section> Sections { get; set; }
}

public class Section
{
    public List<ILinePrinterSection> Sections { get; set; }
}

public interface ILinePrinterSection
{
    string SectionType { get; }
}

public class LinePrinterImageSection : ILinePrinterSection
{
    public string SectionType => "Image";
    public string Image { get; set; }
    public string Alignment { get; set; }
    public string? TextBefore { get; set; }
    public string? TextAfter { get; set; }
    public string Font { get; set; }
}

public class LinePrinterBlankSection : ILinePrinterSection
{
    public string SectionType => "Blank";
    public int Count { get; set; }
}

public class LinePrinterTextSection : ILinePrinterSection
{
    public string SectionType => "Text";
    public LinePrinterTextSectionStyle Style { get; set; }
    public List<string>? Contents { get; set; }
}

public class LinePrinterTextSectionStyle
{
    public List<LinePrinterColumnStyle> Columns { get; set; }
}

public class LinePrinterColumnStyle
{
    public int Start { get; set; }
    public int End { get; set; }
    public string Alignment { get; set; }
    public Font Font { get; set; }
}

public class Font
{
    public string Family { get; set; }
    public string Size { get; set; }
    public string Weight { get; set; }
    public string Style { get; set; }
    public string Width { get; set; }
}