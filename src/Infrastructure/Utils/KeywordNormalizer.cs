using jobAgentApi.Application.Abstractions;

namespace jobAgentApi.Infrastructure.Utils;

public class KeywordNormalizer : IKeywordNormalizer
{
    public List<string> Normalize(IEnumerable<string> keywords)
    {
        if (keywords == null) return new List<string>();

        return keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim().ToLowerInvariant())
            .Distinct()
            .OrderBy(k => k)
            .ToList();
    }
}
