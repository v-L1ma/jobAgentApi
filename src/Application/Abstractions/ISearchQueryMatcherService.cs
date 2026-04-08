using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Abstractions;

public interface ISearchQueryMatcherService
{
    SearchQuery? FindSimilarQuery(List<string> userKeywords, IEnumerable<SearchQuery> existingQueries);
}
