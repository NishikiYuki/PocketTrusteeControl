using System.Text;

namespace PocketTrusteeCLI;

internal static class CommandLineParser {
    public static string[] Split(string input) {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var hasToken = false;

        for (var i = 0; i < input.Length; i++) {
            var character = input[i];
            if (quote != '\0') {
                if (character == quote) {
                    if (i + 1 < input.Length && input[i + 1] == quote) {
                        current.Append(quote);
                        i++;
                    }
                    else {
                        quote = '\0';
                    }
                }
                else {
                    current.Append(character);
                }

                hasToken = true;
                continue;
            }

            if (char.IsWhiteSpace(character)) {
                if (!hasToken) {
                    continue;
                }

                arguments.Add(current.ToString());
                current.Clear();
                hasToken = false;
            }
            else if (character is '"' or '\'') {
                quote = character;
                hasToken = true;
            }
            else {
                current.Append(character);
                hasToken = true;
            }
        }

        if (quote != '\0') {
            throw new FormatException($"Missing closing {quote} quote.");
        }

        if (hasToken) {
            arguments.Add(current.ToString());
        }

        return arguments.ToArray();
    }
}
