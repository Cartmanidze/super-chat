namespace SuperChat.Domain.Services;

public static class StructuredArtifactDetector
{
    private static readonly string[] PromptLeadIns =
    [
        "design a high-fidelity",
        "product goal",
        "target users",
        "core product positioning",
        "main information architecture",
        "visual style",
        "color direction",
        "design system / components",
        "example prompts / quick chips",
        "context for llm",
        "контекст для llm",
        "контекст из чата",
        "--- диалог #",
        "--- dialogue #",
        "примерный дизайн экранов",
        "дизайн-система",
        "информационная архитектура"
    ];

    private static readonly string[] SpecKeywords =
    [
        "desktop web app",
        "responsive mobile",
        "wireframe",
        "screen ",
        "login / invite-only",
        "connect telegram",
        "main today view",
        "search / ask",
        "settings / connection",
        "мобильный вид",
        "экран 1",
        "экран 2",
        "экран 3",
        "экран 4",
        "экран 5",
        "карточка",
        "sidebar",
        "top navigation",
        "bottom navigation",
        "evidence snippet",
        "magic link",
        "invite-only"
    ];

    public static bool LooksLikeStructuredArtifact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        if (normalized.Length < 220)
        {
            return false;
        }

        var lowered = normalized.ToLowerInvariant();
        if (ContainsAny(lowered, PromptLeadIns))
        {
            return true;
        }

        var score = 0;
        if (CountHits(lowered, SpecKeywords) >= 3)
        {
            score++;
        }

        if (HasBoxDrawingCharacters(normalized))
        {
            score++;
        }

        if (CountBulletedLines(normalized) >= 4)
        {
            score++;
        }

        if (CountNumberedSectionLines(normalized) >= 3)
        {
            score++;
        }

        if (HasHighHeadingDensity(normalized))
        {
            score++;
        }

        return score >= 3;
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static int CountHits(string text, IEnumerable<string> values)
    {
        return values.Count(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static bool HasBoxDrawingCharacters(string text)
    {
        return text.IndexOfAny(['┌', '┐', '└', '┘', '│', '─']) >= 0;
    }

    private static int CountBulletedLines(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line =>
                line.StartsWith("●", StringComparison.Ordinal) ||
                line.StartsWith("-", StringComparison.Ordinal) ||
                line.StartsWith("•", StringComparison.Ordinal));
    }

    private static int CountNumberedSectionLines(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line =>
            {
                if (line.Length < 3 || !char.IsDigit(line[0]))
                {
                    return false;
                }

                var separator = line[1];
                return separator is '.' or ')' or ' ';
            });
    }

    private static bool HasHighHeadingDensity(string text)
    {
        var headingCandidates = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Count(line =>
                line.Length is >= 4 and <= 48 &&
                line.EndsWith(":", StringComparison.Ordinal) &&
                line.Count(char.IsWhiteSpace) <= 6);

        return headingCandidates >= 3;
    }
}
