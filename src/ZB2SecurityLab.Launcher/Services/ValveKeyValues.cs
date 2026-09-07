using System.Text;

namespace ZB2SecurityLab.Launcher.Services;

internal sealed class ValveKeyValuesNode
{
    internal Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, ValveKeyValuesNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal ValveKeyValuesNode? Child(string name) => Children.TryGetValue(name, out var child) ? child : null;
    internal string? Value(string name) => Values.TryGetValue(name, out var value) ? value : null;
}

internal static class ValveKeyValues
{
    internal static ValveKeyValuesNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenize(text);
        var position = 0;
        var root = ParseNode(tokens, ref position, expectClosingBrace: false);
        if (position != tokens.Count)
        {
            throw new FormatException("Unexpected trailing VDF tokens.");
        }

        return root;
    }

    private static ValveKeyValuesNode ParseNode(IReadOnlyList<string> tokens, ref int position, bool expectClosingBrace)
    {
        var node = new ValveKeyValuesNode();
        while (position < tokens.Count)
        {
            if (tokens[position] == "}")
            {
                if (!expectClosingBrace)
                {
                    throw new FormatException("Unexpected closing brace in VDF document.");
                }

                position++;
                return node;
            }

            var key = tokens[position++];
            if (position >= tokens.Count)
            {
                throw new FormatException($"Missing value for VDF key '{key}'.");
            }

            if (tokens[position] == "{")
            {
                position++;
                node.Children[key] = ParseNode(tokens, ref position, expectClosingBrace: true);
            }
            else
            {
                if (tokens[position] == "}")
                {
                    throw new FormatException($"Missing value for VDF key '{key}'.");
                }

                node.Values[key] = tokens[position++];
            }
        }

        if (expectClosingBrace)
        {
            throw new FormatException("Unclosed VDF object.");
        }

        return node;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var index = 0;
        while (index < text.Length)
        {
            SkipTrivia(text, ref index);
            if (index >= text.Length)
            {
                break;
            }

            var current = text[index];
            if (current is '{' or '}')
            {
                tokens.Add(current.ToString());
                index++;
                continue;
            }

            if (current == '"')
            {
                tokens.Add(ReadQuoted(text, ref index));
                continue;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not '{' and not '}')
            {
                index++;
            }

            tokens.Add(text[start..index]);
        }

        return tokens;
    }

    private static void SkipTrivia(string text, ref int index)
    {
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                index += 2;
                while (index < text.Length && text[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            break;
        }
    }

    private static string ReadQuoted(string text, ref int index)
    {
        index++;
        var value = new StringBuilder();
        while (index < text.Length)
        {
            var current = text[index++];
            if (current == '"')
            {
                return value.ToString();
            }

            if (current == '\\' && index < text.Length)
            {
                var escaped = text[index++];
                value.Append(escaped is '\\' or '"' ? escaped : $"\\{escaped}");
                continue;
            }

            value.Append(current);
        }

        throw new FormatException("Unterminated quoted VDF value.");
    }
}
