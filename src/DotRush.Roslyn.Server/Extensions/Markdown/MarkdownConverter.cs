using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Extensions;

// https://github.com/OmniSharp/omnisharp-roslyn/blob/ed467d0ad2d877b837380a849f856dcd210d69f7/src/OmniSharp.Roslyn.CSharp/Helpers/MarkdownHelpers.cs#L42
public static partial class MarkdownConverter {
    private static readonly Regex EscapeRegex = MyRegex();

    public static string? Escape(string? markdown) {
        return markdown == null ? null : EscapeRegex.Replace(markdown, @"\$1");
    }

    private const string ContainerStart = nameof(ContainerStart);
    private const string ContainerEnd = nameof(ContainerEnd);

    public static bool StartsWithNewline(this ImmutableArray<TaggedText> taggedParts) {
        return !taggedParts.IsDefaultOrEmpty
                && taggedParts[0].Tag switch { TextTags.LineBreak => true, ContainerStart => true, _ => false };
    }

    public static string TaggedTextToMarkdown(ImmutableArray<TaggedText> taggedParts) {
        var stringBuilder = new StringBuilder();
        TaggedTextToMarkdown(taggedParts, stringBuilder);
        return stringBuilder.ToString();
    }
    public static void TaggedTextToMarkdown(ImmutableArray<TaggedText> taggedParts, StringBuilder stringBuilder) {
        FormattingOptions formattingOptions = new();
        bool isInCodeBlock = false;
        bool brokeLine = true;
        bool afterFirstLine = false;

        for (int i = 0; i < taggedParts.Length; i++) {
            var current = taggedParts[i];

            if (brokeLine) {
                Debug.Assert(!isInCodeBlock);
                brokeLine = false;
                bool canFormatAsBlock = !afterFirstLine;

                if (!canFormatAsBlock) {
                    // If we're on a new line and there are no text parts in the upcoming line, then we
                    // can format the whole line as C# code instead of plaintext. Otherwise, we need to
                    // intermix, and can only use simple ` codefences
                    for (int j = i; j < taggedParts.Length; j++) {
                        switch (taggedParts[j].Tag) {
                            case TextTags.Text:
                                canFormatAsBlock = false;
                                goto endOfLineOrTextFound;

                            case ContainerStart:
                            case ContainerEnd:
                            case TextTags.LineBreak:
                                goto endOfLineOrTextFound;

                            default:
                                // If the block is just newlines, then we don't want to format that as
                                // C# code. So, we default to false, set it to true if there's actually
                                // content on the line, then set to false again if Text content is
                                // encountered.
                                canFormatAsBlock = true;
                                continue;
                        }
                    }
                }
                else {
                    // If it's just a newline, we're going to default to standard handling which will
                    // skip the newline.
                    canFormatAsBlock = !indexIsTag(i, ContainerStart, ContainerEnd, TextTags.LineBreak);
                }

            endOfLineOrTextFound:
                if (canFormatAsBlock) {
                    afterFirstLine = true;
                    stringBuilder.Append("```csharp");
                    stringBuilder.Append(formattingOptions.NewLine);
                    for (; i < taggedParts.Length; i++) {
                        current = taggedParts[i];
                        if (current.Tag == ContainerStart
                            || current.Tag == ContainerEnd
                            || current.Tag == TextTags.LineBreak) {
                            stringBuilder.Append(formattingOptions.NewLine);
                            stringBuilder.Append("```");
                            goto standardHandling;
                        }
                        else {
                            stringBuilder.Append(current.Text);
                        }
                    }

                    // If we're here, that means that the last part has been reached, so just
                    // return.
                    Debug.Assert(i == taggedParts.Length);
                    stringBuilder.Append(formattingOptions.NewLine);
                    stringBuilder.Append("```");
                    return;
                }
            }

        standardHandling:
            switch (current.Tag) {
                case TextTags.Text when !isInCodeBlock:
                    addText(current.Text);
                    break;

                case TextTags.Text:
                    endBlock();
                    addText(current.Text);
                    break;

                case TextTags.Space when isInCodeBlock:
                    if (indexIsTag(i + 1, TextTags.Text))
                        endBlock();

                    addText(current.Text);
                    break;

                case TextTags.Space:
                case TextTags.Punctuation:
                    addText(current.Text);
                    break;

                case ContainerStart:
                    addNewline();
                    addText(current.Text);
                    break;

                case ContainerEnd:
                    addNewline();
                    break;

                case TextTags.LineBreak:
                    if (stringBuilder.Length != 0 && !indexIsTag(i + 1, ContainerStart, ContainerEnd) && i + 1 != taggedParts.Length)
                        addNewline();
                    break;

                default:
                    if (!isInCodeBlock) {
                        isInCodeBlock = true;
                        stringBuilder.Append('`');
                    }
                    stringBuilder.Append(current.Text);
                    brokeLine = false;
                    break;
            }
        }

        if (isInCodeBlock)
            endBlock();

        return;

        void addText(string? text) {
            brokeLine = false;
            afterFirstLine = true;
            if (!isInCodeBlock)
                text = Escape(text);

            stringBuilder.Append(text);
        }

        void addNewline() {
            if (isInCodeBlock)
                endBlock();

            // Markdown needs 2 linebreaks to make a new paragraph
            stringBuilder.Append(formattingOptions.NewLine);
            stringBuilder.Append(formattingOptions.NewLine);
            brokeLine = true;
        }

        void endBlock() {
            stringBuilder.Append('`');
            isInCodeBlock = false;
        }

        bool indexIsTag(int i, params string[] tags) {
            return i < taggedParts.Length && tags.Contains(taggedParts[i].Tag);
        }
    }

    [GeneratedRegex(@"([\\`\*_\{\}\[\]\(\)#+\-\.!])", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
