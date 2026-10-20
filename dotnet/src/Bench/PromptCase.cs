using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bench;

// One entry in prompt.json.
public sealed record PromptCase(int Id, string Kind, string Prompt, Check Check);

public sealed record Check(string Type, string? Expected, string[]? RequiredKeys, string? Rubric)
{
    // true / false = code can grade it.  null = you score it by hand with the rubric.
    public bool? Passes(string answer)
    {
        answer = answer.Trim();

        return Type switch
        {
            "contains" => answer.Contains(Expected!, StringComparison.OrdinalIgnoreCase),
            "number"   => Regex.IsMatch(answer, $@"(?<![\d.]){Regex.Escape(Expected!)}(?!\.?\d)"), // 0.05 must not match 10.05 or 0.055
            "json"     => IsJsonWithKeys(answer, RequiredKeys!),
            "bullets"  => IsThreeBullets(answer),
            "manual"   => null,
            _ => throw new InvalidDataException($"Unknown check type '{Type}'"),
        };
    }

    // Strict on purpose: ```json fences fail, because the prompt said "no Markdown fences".
    private static bool IsJsonWithKeys(string answer, string[] keys)
    {
        try
        {
            using var doc = JsonDocument.Parse(answer);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && keys.All(key => doc.RootElement.TryGetProperty(key, out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsThreeBullets(string answer)
    {
        var lines = answer.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 3 && lines.All(line => line.StartsWith("- "));
    }
}
