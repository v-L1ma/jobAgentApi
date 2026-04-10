using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Infrastructure.Services;

public class SearchQueryService : ISearchQueryService
{
    private readonly IKeywordNormalizer _normalizer;
    private readonly ISearchQueryMatcherService _matcher;
    private readonly IUnitOfWork _unitOfWork;

    public SearchQueryService(IKeywordNormalizer normalizer, ISearchQueryMatcherService matcher, IUnitOfWork unitOfWork)
    {
        _normalizer = normalizer;
        _matcher = matcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<SearchQuery> ProcessQueryAsync(string query, List<string> keywords, string level, string area)
    {
        // 1. Normalizar keywords
        var normalizedKeywords = _normalizer.Normalize(keywords ?? new List<string>());
        
        if (!normalizedKeywords.Any())
        {
            throw new ArgumentException("Keywords are required to process a search query.");
        }

        // 2. Buscar queries existentes (filtrando por level e area)
        var repository = _unitOfWork.GetRepository<SearchQuery>();
        var allQueries = await repository.GetAllAsync();
        
        var matchingQueries = allQueries.Where(q => 
            string.Equals(q.Level, level, StringComparison.OrdinalIgnoreCase) && 
            string.Equals(q.Area, area, StringComparison.OrdinalIgnoreCase));

        // 3. Rodar algoritmo de similaridade
        var existingSimilarQuery = _matcher.FindSimilarQuery(normalizedKeywords, matchingQueries);

        // 4. Se encontrou -> reutiliza
        if (existingSimilarQuery != null)
        {
            return existingSimilarQuery;
        }

        // 5. Se não -> cria nova
        var orderedKeywords = normalizedKeywords.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var normalizedHash = string.Join("-", orderedKeywords); // join das keywords ordenadas

        var newSearchQuery = new SearchQuery
        {
            Query = query,
            Keywords = orderedKeywords, // Usando as ordenadas para consistência
            Level = level,
            Area = area,
            NormalizedHash = normalizedHash,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };

        await repository.AddAsync(newSearchQuery);
        await _unitOfWork.SaveChangesAsync();

        return newSearchQuery;
    }
}
