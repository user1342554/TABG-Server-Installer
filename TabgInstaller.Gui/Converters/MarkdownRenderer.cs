using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TabgInstaller.Gui.Converters;

public static class MarkdownRenderer
{
    // Pre-frozen brushes to avoid per-render allocation and enable cross-thread use
    private static readonly SolidColorBrush CodeBlockBg = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)));
    private static readonly SolidColorBrush CodeBlockBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)));
    private static readonly SolidColorBrush InlineCodeBg = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)));

    private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

    public static IEnumerable<UIElement> RenderMarkdown(string markdown)
    {
        var elements = new List<UIElement>();
        if (string.IsNullOrEmpty(markdown))
            return elements;

        try
        {
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCodeBlock = false;
            var codeBlockLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Code block toggle
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End code block
                        inCodeBlock = false;
                        elements.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines)));
                        codeBlockLines.Clear();
                    }
                    else
                    {
                        // Start code block
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(line);
                    continue;
                }

                // Blank line
                if (string.IsNullOrWhiteSpace(line))
                {
                    elements.Add(new FrameworkElement { Height = 6 });
                    continue;
                }

                // ## Heading (check before # heading)
                if (line.StartsWith("## "))
                {
                    elements.Add(new TextBlock
                    {
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 2),
                        TextWrapping = TextWrapping.Wrap,
                        Text = line.Substring(3).Trim()
                    });
                    continue;
                }

                // # Heading
                if (line.StartsWith("# "))
                {
                    elements.Add(new TextBlock
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 4),
                        TextWrapping = TextWrapping.Wrap,
                        Text = line.Substring(2).Trim()
                    });
                    continue;
                }

                // Bullet list item
                var bulletMatch = Regex.Match(line, @"^\s*[-*]\s+(.*)");
                if (bulletMatch.Success)
                {
                    var tb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    tb.Inlines.Add(new Run("  \u2022 "));
                    AddFormattedInlines(tb.Inlines, bulletMatch.Groups[1].Value);
                    elements.Add(tb);
                    continue;
                }

                // Plain text with inline formatting
                {
                    var tb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    AddFormattedInlines(tb.Inlines, line);
                    elements.Add(tb);
                }
            }

            // If we ended while still in a code block, flush remaining lines
            if (inCodeBlock && codeBlockLines.Count > 0)
            {
                elements.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines)));
            }
        }
        catch
        {
            // Never crash - render as plain text fallback
            elements.Clear();
            elements.Add(new TextBlock
            {
                Text = markdown,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return elements;
    }

    private static TextBox CreateCodeBlock(string code)
    {
        return new TextBox
        {
            Text = code,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            Background = CodeBlockBg,
            Foreground = Brushes.Black,
            BorderThickness = new Thickness(1),
            BorderBrush = CodeBlockBorder,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 4),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };
    }

    private static void AddFormattedInlines(InlineCollection inlines, string text)
    {
        try
        {
            // Pattern handles **bold**, `inline code`, and plain text
            var pattern = @"(\*\*(.+?)\*\*)|(`([^`]+)`)";
            int lastIndex = 0;

            foreach (Match match in Regex.Matches(text, pattern))
            {
                // Add text before match as plain
                if (match.Index > lastIndex)
                {
                    inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                if (match.Groups[2].Success)
                {
                    // **bold**
                    inlines.Add(new Bold(new Run(match.Groups[2].Value)));
                }
                else if (match.Groups[4].Success)
                {
                    // `inline code`
                    var run = new Run(match.Groups[4].Value)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = InlineCodeBg
                    };
                    inlines.Add(run);
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }
        catch
        {
            // Fallback: just add the raw text
            inlines.Add(new Run(text));
        }
    }
}
