using System.Text;
using System.Text.RegularExpressions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Extensions;

public static partial class MarkdownExtensions {
    // https://github.com/dotnet/roslyn/blob/main/src/LanguageServer/Protocol/Extensions/ProtocolConversions.cs
    public static string CreateDocumentation(IEnumerable<TaggedText> taggedParts, string lang = "csharp") {
        var sb = new StringBuilder();
        var codeFence = string.Empty;
        var isIndentation = false;

        foreach (var part in taggedParts) {
            var style = InternalTaggedText.GetStyle(part);
            var isLineEmpty = sb.Length == 0 || sb[^1] == '\n';
            var isCode = codeFence.Length > 0 || style == InternalTaggedText.StyleCode;
            isIndentation = !isCode && (isLineEmpty || isIndentation) && part.Tag != TextTags.LineBreak && part.Text.Length > 0 && string.IsNullOrWhiteSpace(part.Text);

            if (part.Tag == TextTags.LineBreak) {
                // Two trailing spaces keep the line break in the rendered markdown
                sb.AppendLine("  ");
            }
            else if (part.Tag == InternalTextTags.CodeBlockStart) {
                codeFence = isLineEmpty ? "```" : "`";
                sb.Append(isLineEmpty ? codeFence + lang + Environment.NewLine : codeFence).Append(part.Text);
            }
            else if (part.Tag == InternalTextTags.CodeBlockEnd) {
                sb.Append(codeFence == "`" ? codeFence : Environment.NewLine + codeFence + Environment.NewLine).Append(part.Text);
                codeFence = string.Empty;
            }
            else if (part.Tag == TextTags.Text && style == (InternalTaggedText.StyleCode | InternalTaggedText.StylePreserveWhitespace)) {
                // The content of the <code> element
                if (!isLineEmpty)
                    sb.AppendLine("  ");
                sb.AppendLine("```text").AppendLine(part.Text).AppendLine("```");
            }
            else if (isIndentation) {
                // Markdown ignores the leading spaces
                sb.Append(part.Text.Replace(" ", "&nbsp;"));
            }
            else {
                var text = isCode ? part.Text : EscapeRegex().Replace(part.Text, @"\$1");
                var navigationHint = InternalTaggedText.GetNavigationHint(part);
                if (!string.IsNullOrEmpty(navigationHint) && navigationHint == InternalTaggedText.GetNavigationTarget(part))
                    text = $"[{text}]({navigationHint})";

                sb.Append(style switch {
                    InternalTaggedText.StyleStrong => $"**{text}**",
                    InternalTaggedText.StyleEmphasis => $"_{text}_",
                    InternalTaggedText.StyleUnderline => $"<u>{text}</u>",
                    InternalTaggedText.StyleCode => text.Contains('`') ? $"``{text}``" : $"`{text}`",
                    _ => text
                });
            }
        }

        return sb.ToString();
    }

    public static string InjectText(string text, string documentation) {
        // The text is placed on the first line of the leading code block
        if (!documentation.StartsWith("```", StringComparison.Ordinal))
            return string.Concat(text, documentation);

        return documentation.Insert(documentation.IndexOf('\n') + 1, text);
    }

    [GeneratedRegex(@"([\\`\*_\{\}\[\]\(\)#+\-\.!<>])")]
    private static partial Regex EscapeRegex();
}