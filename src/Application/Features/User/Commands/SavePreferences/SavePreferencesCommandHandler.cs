using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace jobAgentApi.Application.Features.User.Commands.SavePreferences;

public sealed class SavePreferencesCommandHandler : ICommandHandler<SavePreferencesCommand, Guid>
{
    private const int MaxSkillsToProcess = 5;
    private const int MaxKeywordLength = 30;
    private const int MaxLevelsToProcess = 4;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISearchQueryService _searchQueryService;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;
    private readonly IKeywordNormalizer _keywordNormalizer;

    public SavePreferencesCommandHandler(
        IUnitOfWork unitOfWork, 
        ISearchQueryService searchQueryService,
        IUserSearchQueryRepository userSearchQueryRepository,
        IKeywordNormalizer keywordNormalizer)
    {
        _unitOfWork = unitOfWork;
        _searchQueryService = searchQueryService;
        _userSearchQueryRepository = userSearchQueryRepository;
        _keywordNormalizer = keywordNormalizer;
    }

    public async Task<Guid> Handle(SavePreferencesCommand request, CancellationToken cancellationToken)
    {
        var searchQueryRepository = _unitOfWork.GetRepository<SearchQuery>();
        var userSearchQueryRepository = _unitOfWork.GetRepository<UserSearchQuery>();

        var skillsToProcess = request.Skills?
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .ToList() ?? new List<string>();

        if (skillsToProcess.Count > MaxSkillsToProcess)
        {
            throw new DomainException($"O limite é de {MaxSkillsToProcess} palavras-chave por usuário.");
        }

        if (skillsToProcess.Any(skill => skill.Length > MaxKeywordLength))
        {
            throw new DomainException($"Cada palavra-chave pode ter no máximo {MaxKeywordLength} caracteres.");
        }

        var normalizedKeywords = _keywordNormalizer.Normalize(skillsToProcess);

        if (!normalizedKeywords.Any())
        {
            throw new DomainException("Pelo menos uma palavra-chave válida é obrigatória para salvar as preferências de busca.");
        }

        var levelsToProcess = request.Levels?
            .Where(level => !string.IsNullOrWhiteSpace(level))
            .Select(level => level.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (!levelsToProcess.Any())
        {
            throw new DomainException("Selecione pelo menos uma senioridade.");
        }

        if (levelsToProcess.Count > MaxLevelsToProcess)
        {
            throw new DomainException($"Selecione no máximo {MaxLevelsToProcess} senioridades.");
        }

        var area = request.Area?.Trim() ?? string.Empty;
        var queryStr = BuildQueryString(skillsToProcess, levelsToProcess);
        var normalizedHash = BuildNormalizedHash(normalizedKeywords, levelsToProcess);

        var currentSearchQueryId = await _userSearchQueryRepository.GetUserCurrentSearchQueryIdAsync(request.UserId, cancellationToken);

        SearchQuery resultQuery;

        if (currentSearchQueryId.HasValue)
        {
            var usersCount = await _userSearchQueryRepository.GetUsersCountBySearchQueryAsync(currentSearchQueryId.Value, cancellationToken);

            if (usersCount <= 1)
            {
                var currentSearchQuery = await searchQueryRepository.GetByIdAsync(currentSearchQueryId.Value);

                if (currentSearchQuery is null)
                {
                    await _userSearchQueryRepository.RemoveUserFromSearchQueryAsync(request.UserId, currentSearchQueryId.Value, cancellationToken);
                    await _userSearchQueryRepository.DeleteOrphanSearchQueryAsync(currentSearchQueryId.Value, cancellationToken);

                    resultQuery = CreateSearchQuery(queryStr, normalizedKeywords, levelsToProcess, area, normalizedHash);
                    await searchQueryRepository.AddAsync(resultQuery);

                    await userSearchQueryRepository.AddAsync(new UserSearchQuery
                    {
                        UserId = request.UserId,
                        SearchQueryId = resultQuery.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    currentSearchQuery.Query = queryStr;
                    currentSearchQuery.Keywords = normalizedKeywords;
                    currentSearchQuery.Levels = levelsToProcess;
                    currentSearchQuery.Area = area;
                    currentSearchQuery.NormalizedHash = normalizedHash;
                    currentSearchQuery.LastModifiedAt = DateTime.UtcNow;

                    await searchQueryRepository.UpdateAsync(currentSearchQuery);
                    resultQuery = currentSearchQuery;
                }
            }
            else
            {
                await _userSearchQueryRepository.RemoveUserFromSearchQueryAsync(request.UserId, currentSearchQueryId.Value, cancellationToken);
                await _userSearchQueryRepository.DeleteOrphanSearchQueryAsync(currentSearchQueryId.Value, cancellationToken);

                resultQuery = CreateSearchQuery(queryStr, normalizedKeywords, levelsToProcess, area, normalizedHash);
                await searchQueryRepository.AddAsync(resultQuery);

                await userSearchQueryRepository.AddAsync(new UserSearchQuery
                {
                    UserId = request.UserId,
                    SearchQueryId = resultQuery.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            // Mantém reaproveitamento de queries similares quando o usuário ainda não possui vínculo.
            resultQuery = await _searchQueryService.ProcessQueryAsync(queryStr, skillsToProcess, levelsToProcess, area);

            await userSearchQueryRepository.AddAsync(new UserSearchQuery
            {
                UserId = request.UserId,
                SearchQueryId = resultQuery.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultQuery.Id;
    }

    private static string BuildQueryString(IEnumerable<string> skills, IEnumerable<string> levels)
    {
        var keywordExpression = string.Join(" OR ", skills.Where(skill => !string.IsNullOrWhiteSpace(skill)));
        var levelExpression = string.Join(" OR ", levels.Where(level => !string.IsNullOrWhiteSpace(level)));
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(keywordExpression))
        {
            queryParts.Add($"({keywordExpression})");
        }

        if (!string.IsNullOrWhiteSpace(levelExpression))
        {
            queryParts.Add($"({levelExpression})");
        }

        return string.Join(" AND ", queryParts);
    }

    private static string BuildNormalizedHash(IEnumerable<string> normalizedKeywords, IEnumerable<string> levels)
    {
        var orderedKeywords = normalizedKeywords
            .OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase);

        var orderedLevels = levels
            .Where(level => !string.IsNullOrWhiteSpace(level))
            .Select(level => level.Trim().ToLowerInvariant())
            .OrderBy(level => level, StringComparer.Ordinal);

        return string.Join("|", new[]
        {
            string.Join("-", orderedKeywords),
            string.Join("-", orderedLevels)
        });
    }

    private static SearchQuery CreateSearchQuery(
        string query,
        List<string> normalizedKeywords,
        List<string> levels,
        string area,
        string normalizedHash)
    {
        return new SearchQuery
        {
            Id = Guid.NewGuid(),
            Query = query,
            Keywords = normalizedKeywords,
            Levels = levels,
            Area = area,
            NormalizedHash = normalizedHash,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastExecutedAt = DateTime.MinValue
        };
    }
}
