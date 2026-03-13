using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static partial class LexicalSparseVectorBuilder
{
    [GeneratedRegex(@"\w+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    public static SparseTextVector Build(string text)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new SparseTextVector([], []);
        }

        var matches = TokenPattern().Matches(normalized);
        if (matches.Count == 0)
        {
            return new SparseTextVector([], []);
        }

        var weights = new Dictionary<long, float>();
        foreach (Match match in matches)
        {
            var token = match.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var index = StableSparseIndex(token);
            var contribution = 1f / matches.Count;
            weights[index] = weights.TryGetValue(index, out var existing)
                ? existing + contribution
                : contribution;
        }

        var ordered = weights
            .OrderBy(pair => pair.Key)
            .ToArray();

        return new SparseTextVector(
            ordered.Select(pair => pair.Key).ToArray(),
            ordered.Select(pair => pair.Value).ToArray());
    }

    private static string Normalize(string text)
    {
        return string.Join(" ", text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static long StableSparseIndex(string text)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(text));
        return BitConverter.ToUInt32(bytes, 0);
    }
}
