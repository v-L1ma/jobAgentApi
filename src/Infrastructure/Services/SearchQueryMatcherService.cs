using jobAgentApi.Application.Abstractions;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Infrastructure.Services;

public class SearchQueryMatcherService : ISearchQueryMatcherService
{
    public SearchQuery? FindSimilarQuery(List<string> userKeywords, IEnumerable<SearchQuery> existingQueries)
    {
        if (userKeywords == null || !userKeywords.Any()) return null;

        foreach (var query in existingQueries)
        {
            if (query.Keywords == null) continue;

            var minMatch = userKeywords.Count <= 2 ? userKeywords.Count : 3;
            var intersectionSize = userKeywords.Intersect(query.Keywords, StringComparer.OrdinalIgnoreCase).Count();

            if (intersectionSize >= minMatch)
            {
                return query;
            }
        }

        return null;
    }
}
