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

    public async Task<SearchQuery> ProcessQueryAsync(string query, List<string> keywords, string level, string area)
    {
        // 1. Normalizar keywords
        var normalizedKeywords = _normalizer.Normalize(keywords ?? new List<string>());
        
        if (!normalizedKeywords.Any())
        {
            throw new ArgumentException("Keywords are required to process a search query.");
        }

        // 2. Buscar queries existentes com filtro no banco (evita carregar tudo em memória)
        var normalizedLevel = (level ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedArea = (area ?? string.Empty).Trim().ToLowerInvariant();

        var matchingQueries = await _dbContext.SearchQueries
            .AsNoTracking()
            .Where(q => q.Active)
            .Where(q => q.Level != null && q.Area != null)
            .Where(q => q.Level!.ToLower() == normalizedLevel && q.Area!.ToLower() == normalizedArea)
            .OrderByDescending(q => q.LastModifiedAt)
            .Take(MaxMatchingQueries)
            .ToListAsync();

        // 3. Rodar algoritmo de similaridade
        var existingSimilarQuery = _matcher.FindSimilarQuery(normalizedKeywords, matchingQueries);

        // 4. Se encontrou -> reutiliza
        if (existingSimilarQuery != null)
        {
            return existingSimilarQuery;
        }

        // 5. Se não -> cria nova
        var repository = _unitOfWork.GetRepository<SearchQuery>();
        var orderedKeywords = normalizedKeywords.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var normalizedHash = string.Join("-", orderedKeywords); // join das keywords ordenadas

        var newSearchQuery = new SearchQuery
        {
            Query = query,
            Keywords = orderedKeywords, // Usando as ordenadas para consistência
            Level = level ?? string.Empty,
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
}
