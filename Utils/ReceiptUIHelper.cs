using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Qatalyst.Objects;

namespace Qatalyst.Utils
{
    public class ReceiptUIHelper
    {
        /// <summary>
        /// Generates controls from the Receipt object and adds them to the ReceiptStackPanel.
        /// </summary>
        /// <param name="receipt">The Receipt object.</param>
        /// <param name="stackPanel">The StackPanel to populate with controls.</param>
        public static void PopulateReceiptStackPanel(List<string> receipt, StackPanel stackPanel)
        {
            if (receipt.Count <= 0) return;

            var image = CreateImageControl();
            stackPanel.Children.Add(image);

            CreateBlankLines(1, stackPanel);

            foreach (var line in receipt)
            {
                var alignCenter = line.StartsWith('\t') && line.EndsWith('\t');
                var content = line.Split('\t', StringSplitOptions.RemoveEmptyEntries).ToList();
                var input = content.Count switch
                {
                    3 => AlignThreeColumns(content, 20),
                    2 => AlignTwoColumns(content[0], content[1], 20),
                    1 => !alignCenter || content[0].Length >= 20
                        ? content[0]
                        : AlignOneColumnCenter(content[0], 20),
                    _ => string.Empty
                };
                var textBlock = CreateTextBlock(input);
                stackPanel.Children.Add(textBlock);
            }

            CreateBlankLines(3, stackPanel);
        }

        public static void PopulateReceiptStackPanel(
            Receipt receipt,
            StackPanel rootPanel,
            RightTappedEventHandler rightTappedHandler
        )
        {
            if (receipt?.LinePrintData?.Sections == null) return;

            var stackPanel = new StackPanel()
            {
                Name = receipt.Name,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFC, 0xFC, 0xFC)),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10),
            };
            stackPanel.RightTapped += rightTappedHandler;

            CreateBlankLines(1, stackPanel);

            foreach (var sectionLine in receipt.LinePrintData.Sections.SelectMany(section => section.Sections))
            {
                switch (sectionLine)
                {
                    case LinePrinterImageSection:
                    {
                        var image = CreateImageControl();
                        stackPanel.Children.Add(image);
                        break;
                    }

                    case LinePrinterTextSection { Contents.Count: > 0 } textSection:
                    {
                        var columnCount = textSection.Style.Columns.Count;
                        var contentList = SplitByMaxCount(textSection.Contents, columnCount);

                        foreach (var content in contentList)
                        {
                            var input = columnCount switch
                            {
                                3 => AlignThreeColumns(content, 20),
                                2 => AlignTwoColumns(content[0], content[1], 20),
                                1 => textSection.Style.Columns[0].Alignment switch
                                {
                                    "CENTER" => AlignOneColumnCenter(content[0], 20),
                                    "RIGHT" => AlignOneColumnRight(content[0], 20),
                                    _ => content[0]
                                },
                                _ => string.Empty
                            };
                            var textBlock = CreateTextBlock(input);
                            stackPanel.Children.Add(textBlock);
                        }

                        break;
                    }

                    case LinePrinterBlankSection blank:
                        CreateBlankLines(blank.Count, stackPanel);
                        break;
                }
            }

            CreateBlankLines(1, stackPanel);

            rootPanel.Children.Add(stackPanel);
        }

        /// <summary>
        /// Creates a TextBlock control for a line of text.
        /// </summary>
        private static TextBlock CreateTextBlock(string text, string alignment = "NONE")
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Colors.Black),
                Margin = new Thickness(0, 0, 0, 0)
            };

            switch (alignment?.ToUpperInvariant())
            {
                case "CENTER":
                    textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case "RIGHT":
                    textBlock.HorizontalAlignment = HorizontalAlignment.Right;
                    textBlock.TextAlignment = TextAlignment.Right;
                    break;
                default:
                    textBlock.HorizontalAlignment = HorizontalAlignment.Left;
                    textBlock.TextAlignment = TextAlignment.Left;
                    break;
            }

            return textBlock;
        }

        private static void CreateBlankLines(int count, StackPanel stackPanel)
        {
            for (int i = 0; i < count; i++)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = string.Empty,
                    FontSize = 14,
                    FontFamily = new FontFamily("Consolas")
                });
            }
        }

        /// <summary>
        /// Creates an Image control from a LinePrintImage object.
        /// </summary>
        private static Image CreateImageControl()
        {
            var bitmapImage = new BitmapImage();
            var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Receipts", "paymark.dib");

            if (!File.Exists(assetsPath))
                throw new FileNotFoundException("The specified file was not found.", assetsPath);

            bitmapImage.UriSource = new Uri(assetsPath, UriKind.Absolute);

            // Create the Image control
            var imageControl = new Image
            {
                Source = bitmapImage,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Stretch = Stretch.Uniform
            };

            imageControl.Width = 153.9453125;

            return imageControl;
        }

        private static string AlignOneColumnCenter(string text, int totalWidth)
        {
            var spaceCount = totalWidth - text.Length;
            if (spaceCount <= 0) return text;

            var leftPad = spaceCount / 2;
            var rightPad = spaceCount - leftPad;
            return $"{new string(' ', leftPad)}{text}{new string(' ', rightPad)}";
        }

        private static string AlignOneColumnRight(string text, int totalWidth)
        {
            var spaceCount = totalWidth - text.Length;
            return spaceCount <= 0 ? text : $"{new string(' ', spaceCount)}{text}";
        }

        private static string AlignTwoColumns(string left, string right, int totalWidth)
        {
            var spaceCount = totalWidth - (left.Length + right.Length);
            if (spaceCount < 0) spaceCount = 0; // Prevent negative spaces
            return $"{left}{new string(' ', spaceCount)}{right}";
        }

        private static string AlignThreeColumns(List<string> columns, int totalWidth)
        {
            if (columns.Count != 3) return string.Join(" ", columns);

            var usedWidth = columns[0].Length + columns[1].Length + columns[2].Length;
            var spaceCount = totalWidth - usedWidth;
            if (spaceCount < 0) spaceCount = 0; // Prevent negative spaces

            var leftSpace = spaceCount / 2;
            var rightSpace = spaceCount - leftSpace;

            return $"{columns[0]}{new string(' ', leftSpace)}{columns[1]}{new string(' ', rightSpace)}{columns[2]}";
        }

        public static List<List<string>> SplitByMaxCount(List<string> list, int maxCount)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list), "List cannot be null.");

            if (maxCount <= 0)
                throw new ArgumentException("Max count must be greater than zero.", nameof(maxCount));

            var result = new List<List<string>>();
            for (int i = 0; i < list.Count; i += maxCount)
            {
                result.Add(list.GetRange(i, Math.Min(maxCount, list.Count - i)));
            }

            return result;
        }
    }
}
