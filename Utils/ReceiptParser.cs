using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qatalyst.Objects;

namespace Qatalyst.Utils;

public class ReceiptParser
{
    private static List<string> _receipts = [];

    public static List<string> ParseFromBuffer(List<string> lines)
    {
        var sanitizedInput = lines.Where(ContainsBadKeywords).ToList();
        return sanitizedInput;
    }

    private static bool ContainsBadKeywords(string line)
    {
        return !line.Contains("PrinterImpl") &&
               !line.Contains("printReceiptLog");
    }

    public static Receipt? ParseFromJson(List<string> lines)
    {
        try
        {
            var inputString = string.Join(string.Empty, lines);
            inputString = inputString.Replace("PrinterImpl:savePreview:168 savePreview: receipt: ", string.Empty);
            var output = ParseReceipt(inputString);

            var tran = inputString.Split("TRAN, ", 2).Last()[..6];
            tran = inputString.Contains("MERCHANT COPY") ? tran + "M" : inputString.Contains("CUSTOMER COPY") ? tran + "C" : tran;

            Console.WriteLine($"TRAN: {tran}");
            output.Name = tran;

            if (_receipts.Contains(tran)) return null;

            _receipts.Add(tran);
            return output;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private static Receipt? ParseFromJson2(string inputString)
    {
        try
        {
            if (string.IsNullOrEmpty(inputString))
                throw new ArgumentException("JSON content cannot be null or empty", nameof(inputString));

            var receipt = JsonConvert.DeserializeObject<Receipt>(inputString);

            if (receipt == null)
                throw new InvalidOperationException("Failed to parse the JSON into a Receipt object.");

            return receipt;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Receipt ParseReceipt(string input)
    {
        // Initialize the receipt
        var receipt = new Receipt
        {
            LinePrintData = new LinePrintData
            {
                Sections = new List<Section>()
            }
        };

        // Extract the sections content
        var sectionsMatch = Regex.Match(input, @"LinePrinterReceipt\(sections=\[(.*)\]\)");
        if (!sectionsMatch.Success)
        {
            throw new Exception("Invalid input format.");
        }

        var sectionsContent = sectionsMatch.Groups[1].Value;

        // Split the sections
        var sectionsList = SplitSections(sectionsContent);

        // Parse each section
        foreach (var section in sectionsList.Select(sectionStr => ParseSection(sectionStr.Trim()))
                     .OfType<ILinePrinterSection>())
        {
            receipt.LinePrintData.Sections.Add(new Section
            {
                Sections = [section]
            });
        }

        return receipt;
    }

    private static List<string> SplitSections(string input)
    {
        var sections = new List<string>();
        var bracketLevel = 0;
        var startIndex = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            switch (c)
            {
                case '(' or '[':
                    bracketLevel++;
                    break;
                case ')' or ']':
                    bracketLevel--;
                    break;
                case ',' when bracketLevel == 0:
                    // Split here
                    sections.Add(input.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                    break;
            }
        }

        sections.Add(input[startIndex..]);

        return sections;
    }

    private static ILinePrinterSection? ParseSection(string sectionStr)
    {
        return sectionStr switch
        {
            not null when sectionStr.StartsWith("LinePrinterImageSection") => ParseLinePrinterImageSection(sectionStr),
            not null when sectionStr.StartsWith("LinePrinterBlankSection") => ParseLinePrinterBlankSection(sectionStr),
            not null when sectionStr.StartsWith("LinePrinterTextSection") => ParseLinePrinterTextSection(sectionStr),
            _ => null
        };
    }

    private static LinePrinterImageSection ParseLinePrinterImageSection(string input)
    {
        var section = new LinePrinterImageSection();

        var alignmentMatch = Regex.Match(input, @"alignment=([A-Z]+)");
        if (alignmentMatch.Success)
        {
            section.Alignment = alignmentMatch.Groups[1].Value;
        }

        var textBeforeMatch = Regex.Match(input, @"textBefore=([^,]+)");
        if (textBeforeMatch.Success)
        {
            section.TextBefore = textBeforeMatch.Groups[1].Value == "null" ? null : textBeforeMatch.Groups[1].Value;
        }

        var textAfterMatch = Regex.Match(input, @"textAfter=([^,]+)");
        if (textAfterMatch.Success)
        {
            section.TextAfter = textAfterMatch.Groups[1].Value == "null" ? null : textAfterMatch.Groups[1].Value;
        }

        return section;
    }

    private static LinePrinterBlankSection ParseLinePrinterBlankSection(string input)
    {
        var section = new LinePrinterBlankSection();

        var countMatch = Regex.Match(input, @"count=(\d+)");
        if (countMatch.Success)
        {
            section.Count = int.Parse(countMatch.Groups[1].Value);
        }

        return section;
    }

    private static LinePrinterTextSection ParseLinePrinterTextSection(string input)
    {
        var section = new LinePrinterTextSection();

        // Parse style
        var styleMatch = Regex.Match(input, @"style=LinePrinterTextSectionStyle\(columns=\[(.*)\]\)");
        if (styleMatch.Success)
        {
            var columnsContent = styleMatch.Groups[1].Value;
            section.Style = new LinePrinterTextSectionStyle
            {
                Columns = ParseColumns(columnsContent)
            };
        }

        // Parse contents
        var contentsMatch = Regex.Match(input, @"contents=\[(.*)\]\)");
        if (contentsMatch.Success)
        {
            var contentsContent = contentsMatch.Groups[1].Value;
            section.Contents = ParseContents(contentsContent);
        }

        return section;
    }

    private static List<LinePrinterColumnStyle> ParseColumns(string input)
    {
        var columns = new List<LinePrinterColumnStyle>();

        // Split the columns
        List<string> columnsList = SplitColumns(input);

        foreach (var columnStr in columnsList)
        {
            var column = new LinePrinterColumnStyle();

            // Parse start
            var startMatch = Regex.Match(columnStr, @"start=(\d+)");
            if (startMatch.Success)
            {
                column.Start = int.Parse(startMatch.Groups[1].Value);
            }

            // Parse end
            var endMatch = Regex.Match(columnStr, @"end=(\d+)");
            if (endMatch.Success)
            {
                column.End = int.Parse(endMatch.Groups[1].Value);
            }

            // Parse alignment
            var alignmentMatch = Regex.Match(columnStr, @"alignment=([A-Z]+)");
            if (alignmentMatch.Success)
            {
                column.Alignment = alignmentMatch.Groups[1].Value;
            }

            // Parse font
            var fontMatch = Regex.Match(columnStr,
                @"font=Font\(family=([^,]+), size=([^,]+), weight=([^,]+), style=([^,]+), width=([^)]+)\)");
            if (fontMatch.Success)
            {
                column.Font = new Font
                {
                    Family = fontMatch.Groups[1].Value,
                    Size = fontMatch.Groups[2].Value,
                    Weight = fontMatch.Groups[3].Value,
                    Style = fontMatch.Groups[4].Value,
                    Width = fontMatch.Groups[5].Value
                };
            }

            columns.Add(column);
        }

        return columns;
    }

    private static List<string> SplitColumns(string input)
    {
        var columns = new List<string>();
        var bracketLevel = 0;
        var startIndex = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '(' || c == '[')
            {
                bracketLevel++;
            }
            else if (c == ')' || c == ']')
            {
                bracketLevel--;
            }
            else if (c == ',' && bracketLevel == 0)
            {
                // Split here
                columns.Add(input.Substring(startIndex, i - startIndex));
                startIndex = i + 1;
            }
        }

        // Add the last column
        columns.Add(input[startIndex..]);

        return columns;
    }

    private static List<string> ParseContents(string input)
    {
        var contents = new List<string>();

        // Split contents by comma
        var items = Regex.Split(input, @",\s*");
        foreach (var item in items)
        {
            contents.Add(item.Trim());
        }

        return contents;
    }
}