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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISearchQueryService _searchQueryService;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public SavePreferencesCommandHandler(
        IUnitOfWork unitOfWork, 
        ISearchQueryService searchQueryService,
        IUserSearchQueryRepository userSearchQueryRepository)
    {
        _unitOfWork = unitOfWork;
        _searchQueryService = searchQueryService;
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<Guid> Handle(SavePreferencesCommand request, CancellationToken cancellationToken)
    {
        var preferencesRepository = _unitOfWork.GetRepository<UserPreferences>();

        var allPreferences = await preferencesRepository.GetAllAsync();
        var existingPreferences = allPreferences.FirstOrDefault(p => p.UserId == request.UserId);

        if (existingPreferences is not null)
        {
            existingPreferences.Skills = request.Skills;
            existingPreferences.Level = request.Level;
            existingPreferences.Area = request.Area;
            existingPreferences.LastModifiedAt = DateTime.UtcNow;

            await preferencesRepository.UpdateAsync(existingPreferences);
        }
        else
        {
            existingPreferences = new UserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Skills = request.Skills,
                Level = request.Level,
                Area = request.Area,
                CreatedAt = DateTime.UtcNow,
                Active = true
            };

            await preferencesRepository.AddAsync(existingPreferences);
        }

        // Limitar palavras-chave para o top 5 (Regra: Muitas keywords -> usar somento o top 5)
        var skillsToProcess = request.Skills?.Take(5).ToList() ?? new List<string>();

        var keywordExpression = string.Join(" OR ", skillsToProcess.Where(skill => !string.IsNullOrWhiteSpace(skill)));
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(keywordExpression))
        {
            queryParts.Add($"({keywordExpression})");
        }

        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            queryParts.Add($"({request.Level})");
        }

        var queryStr = string.Join(" AND ", queryParts);

        // Fase: Garantir que o usuário tenha apenas UMA search query
        var userSearchQueryRepository = _unitOfWork.GetRepository<UserSearchQuery>();
        var currentSearchQueryId = await _userSearchQueryRepository.GetUserCurrentSearchQueryIdAsync(request.UserId, cancellationToken);

        // Se o usuário já tem uma query, verificar se precisa ser removido dela
        if (currentSearchQueryId.HasValue)
        {
            // Verifica quantos usuários estão usando a query atual
            var usersCount = await _userSearchQueryRepository.GetUsersCountBySearchQueryAsync(currentSearchQueryId.Value, cancellationToken);

            // Remove o usuário da query atual
            await _userSearchQueryRepository.RemoveUserFromSearchQueryAsync(request.UserId, currentSearchQueryId.Value, cancellationToken);

            // Se não há outros usuários usando essa query, deleta ela
            if (usersCount <= 1) // <= 1 porque o usuário atual ainda está contando
            {
                await _userSearchQueryRepository.DeleteOrphanSearchQueryAsync(currentSearchQueryId.Value, cancellationToken);
            }
        }

        // Phase 4 & 6: Fluxo completo -> normaliza, busca, tenta match, cria ou reutiliza
        var resultQuery = await _searchQueryService.ProcessQueryAsync(queryStr, skillsToProcess, request.Level, request.Area);

        // Relacionar usuário à nova query (sempre será uma nova associação)
        if (resultQuery != null)
        {
            await userSearchQueryRepository.AddAsync(new UserSearchQuery
            {
                UserId = request.UserId,
                SearchQueryId = resultQuery.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return existingPreferences.Id;
    }
}
