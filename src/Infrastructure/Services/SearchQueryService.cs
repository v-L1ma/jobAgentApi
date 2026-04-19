using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using jobAgentApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace jobAgentApi.Infrastructure.Services;

public class SearchQueryService : ISearchQueryService
{
    private const int MaxMatchingQueries = 200;

    private readonly IKeywordNormalizer _normalizer;
    private readonly ISearchQueryMatcherService _matcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _dbContext;

    public SearchQueryService(
        IKeywordNormalizer normalizer,
        ISearchQueryMatcherService matcher,
        IUnitOfWork unitOfWork,
        AppDbContext dbContext)
    {
        _normalizer = normalizer;
        _matcher = matcher;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
    }

    public async Task<SearchQuery> ProcessQueryAsync(string query, List<string> keywords, List<string> levels, string area)
    {
        // 1. Normalizar keywords
        var normalizedKeywords = _normalizer.Normalize(keywords ?? new List<string>());
        var normalizedLevels = NormalizeLevels(levels);
        
        if (!normalizedKeywords.Any())
        {
            throw new ArgumentException("Keywords are required to process a search query.");
        }

        if (!normalizedLevels.Any())
        {
            throw new ArgumentException("At least one seniority level is required to process a search query.");
        }

        // 2. Buscar queries existentes com filtro no banco (evita carregar tudo em memória)
        var normalizedArea = (area ?? string.Empty).Trim().ToLowerInvariant();

        var matchingQueries = await _dbContext.SearchQueries
            .AsNoTracking()
            .Where(q => q.Active)
            .Where(q => q.Area != null)
            .Where(q => q.Area!.ToLower() == normalizedArea)
            .OrderByDescending(q => q.LastModifiedAt)
            .Take(MaxMatchingQueries)
            .ToListAsync();

        var matchingQueriesWithSameLevels = matchingQueries
            .Where(queryItem => HaveSameLevels(queryItem.Levels, normalizedLevels))
            .ToList();

        // 3. Rodar algoritmo de similaridade
        var existingSimilarQuery = _matcher.FindSimilarQuery(normalizedKeywords, matchingQueriesWithSameLevels);

        // 4. Se encontrou -> reutiliza
        if (existingSimilarQuery != null)
        {
            return existingSimilarQuery;
        }

        // 5. Se não -> cria nova
        var repository = _unitOfWork.GetRepository<SearchQuery>();
        var orderedKeywords = normalizedKeywords.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var normalizedHash = BuildNormalizedHash(orderedKeywords, normalizedLevels);

        var newSearchQuery = new SearchQuery
        {
            Query = query,
            Keywords = orderedKeywords, // Usando as ordenadas para consistência
            Levels = normalizedLevels,
            Area = area ?? string.Empty,
            NormalizedHash = normalizedHash,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };

        await repository.AddAsync(newSearchQuery);
        await _unitOfWork.SaveChangesAsync();

        return newSearchQuery;
    }

    private static List<string> NormalizeLevels(IEnumerable<string>? levels)
    {
        return levels?
            .Where(level => !string.IsNullOrWhiteSpace(level))
            .Select(level => level.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(level => level, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }

    private static bool HaveSameLevels(IEnumerable<string>? queryLevels, IEnumerable<string> requestedLevels)
    {
        var normalizedQueryLevels = NormalizeLevels(queryLevels);
        var normalizedRequestedLevels = NormalizeLevels(requestedLevels);

        if (normalizedQueryLevels.Count != normalizedRequestedLevels.Count)
        {
            return false;
        }

        for (var i = 0; i < normalizedQueryLevels.Count; i++)
        {
            if (!string.Equals(normalizedQueryLevels[i], normalizedRequestedLevels[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildNormalizedHash(IEnumerable<string> orderedKeywords, IEnumerable<string> orderedLevels)
    {
        var keywordsHash = string.Join("-", orderedKeywords);
        var levelsHash = string.Join("-", orderedLevels.Select(level => level.Trim().ToLowerInvariant()));

        return string.Join("|", new[] { keywordsHash, levelsHash });
    }
}
