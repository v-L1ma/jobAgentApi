using System.Collections.Generic;
using System.Threading.Tasks;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Abstractions;

public interface ISearchQueryService
{
    Task<SearchQuery> ProcessQueryAsync(string query, List<string> keywords, List<string> levels, string area);
}
