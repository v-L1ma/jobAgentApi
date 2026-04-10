using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace jobAgentApi.Infrastructure.Repositories;

internal sealed class JobRepository : IJobRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<JobRepository> _logger;

    public JobRepository(AppDbContext dbContext, ILogger<JobRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Job?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<Job?> GetByPlataformJobIdOrUrlAsync(string plataformJobId, string url, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT ""Id"", ""PlataformJobId"", ""Title"", ""Description"", ""Url"", ""IsApplied"", ""Status"", 
                   ""Active"", ""CreatedBy"", ""CreatedAt"", ""LastModifiedBy"", ""LastModifiedAt""
            FROM ""Jobs""
            WHERE ""PlataformJobId"" = @p0 OR ""Url"" = @p1
            LIMIT 1";

        try
        {
            return await _dbContext.Jobs
                .FromSqlRaw(sql, plataformJobId, url)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar job por PlataformJobId={PlataformJobId} ou Url={Url}", plataformJobId, url);
            return null;
        }
    }

    public async Task<int> CountJobsCreatedTodayAsync(DateTime dayStart, DateTime nextDay, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT COUNT(*)::int
            FROM ""Jobs""
            WHERE ""CreatedAt"" >= @p0 AND ""CreatedAt"" < @p1";

        try
        {
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var param1 = command.CreateParameter();
            param1.ParameterName = "@p0";
            param1.Value = dayStart;
            command.Parameters.Add(param1);
            
            var param2 = command.CreateParameter();
            param2.ParameterName = "@p1";
            param2.Value = nextDay;
            command.Parameters.Add(param2);

            if (_dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            {
                await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is int count ? count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao contar jobs criados hoje");
            throw;
        }
    }

    public async Task<(List<Job> Items, int TotalCount)> GetPagedAsync(
        string? query,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Constrói query dinâmica com filtros
        var parameters = new List<NpgsqlParameter>();
        var paramIndex = 0;

        var hasUserFilter = userId.HasValue && userId.Value != Guid.Empty;

        // Base FROM
        var fromSql = @"FROM ""Jobs"" j";

        // Cláusulas WHERE
        var whereParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            whereParts.Add($@"(j.""Title"" ILIKE @p{paramIndex} OR j.""Description"" ILIKE @p{paramIndex})");
            parameters.Add(new NpgsqlParameter($"@p{paramIndex}", $"%{query}%"));
            paramIndex++;
        }

        // Filtra por match nas keywords da search query do usuário
        if (hasUserFilter)
        {
            var userFilterId = userId!.Value;

            // Só aplica filtro de keyword se o usuário tiver keywords válidas.
            // Caso não tenha, esse bloco vira verdadeiro e não bloqueia o retorno de vagas.
            whereParts.Add($@"(
                NOT EXISTS (
                    SELECT 1
                    FROM ""UserSearchQueries"" usq
                    INNER JOIN ""SearchQueries"" sq ON sq.""Id"" = usq.""SearchQueryId""
                    WHERE usq.""UserId"" = @p{paramIndex}
                      AND sq.""Keywords"" IS NOT NULL
                      AND array_length(sq.""Keywords"", 1) > 0
                )
                OR EXISTS (
                    SELECT 1
                    FROM ""UserSearchQueries"" usq
                    INNER JOIN ""SearchQueries"" sq ON sq.""Id"" = usq.""SearchQueryId""
                    INNER JOIN LATERAL unnest(sq.""Keywords"") AS kw ON TRUE
                    WHERE usq.""UserId"" = @p{paramIndex}
                      AND (
                          j.""Title"" ILIKE '%' || kw || '%'
                          OR j.""Description"" ILIKE '%' || kw || '%'
                      )
                )
            )");
            parameters.Add(new NpgsqlParameter($"@p{paramIndex}", userFilterId));
            paramIndex++;
        }

        var whereSql = whereParts.Count > 0 ? " WHERE " + string.Join(" AND ", whereParts) : string.Empty;

        // Colunas SELECT
        var columns = @"j.""Id"", j.""PlataformJobId"", j.""Title"", j.""Description"", j.""Url"",
                   j.""IsApplied"", j.""Status"", j.""Active"", j.""CreatedBy"", j.""CreatedAt"",
                   j.""LastModifiedBy"", j.""LastModifiedAt""";

        // Query de contagem
        var countSql = $@"SELECT COUNT(*)::int {fromSql}{whereSql}";

        // Query paginada
        var dataSql = $@"SELECT {columns} {fromSql}{whereSql} ORDER BY j.""CreatedAt"" DESC LIMIT @p{paramIndex} OFFSET @p{paramIndex + 1}";
        parameters.Add(new NpgsqlParameter($"@p{paramIndex}", pageSize));
        parameters.Add(new NpgsqlParameter($"@p{paramIndex + 1}", (page - 1) * pageSize));

        Console.WriteLine("================================================");
        Console.WriteLine("================================================");
        Console.WriteLine("================================================");
        Console.WriteLine("================================================");
        Console.WriteLine("SQL Gerada:");
        Console.WriteLine(dataSql);
        Console.WriteLine("Parâmetros:");
        foreach (var param in parameters)        {
            Console.WriteLine($"{param.ParameterName}: {param.Value}");
        }
        Console.WriteLine("================================================");
        Console.WriteLine("================================================");
        Console.WriteLine("================================================");
        Console.WriteLine("================================================");


        try
        {
            // Executa count
            var totalCount = 0;
            await using (var countCommand = _dbContext.Database.GetDbConnection().CreateCommand())
            {
                countCommand.CommandText = countSql;
                countCommand.Parameters.AddRange(parameters.ToArray());

                if (_dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                {
                    await _dbContext.Database.OpenConnectionAsync(cancellationToken);
                }

                var result = await countCommand.ExecuteScalarAsync(cancellationToken);
                totalCount = result is int count ? count : 0;
            }

            // Executa query paginada
            var items = new List<Job>();
            await using (var dataCommand = _dbContext.Database.GetDbConnection().CreateCommand())
            {
                dataCommand.CommandText = dataSql;
                dataCommand.Parameters.AddRange(parameters.ToArray());

                await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(MapJobFromReader(reader));
                }
            }

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar query paginada de jobs. SQL: {Sql}", dataSql);
            throw;
        }
    }

    public Task<bool> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Jobs.Add(job);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar job");
            return Task.FromResult(false);
        }
    }

    public Task<bool> UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Jobs.Update(job);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar job");
            return Task.FromResult(false);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Job MapJobFromReader(DbDataReader reader)
    {
        return new Job
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            PlataformJobId = reader.GetString(reader.GetOrdinal("PlataformJobId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Url = reader.GetString(reader.GetOrdinal("Url")),
            IsApplied = reader.GetBoolean(reader.GetOrdinal("IsApplied")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Active = reader.GetBoolean(reader.GetOrdinal("Active")),
            CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            LastModifiedBy = reader.GetString(reader.GetOrdinal("LastModifiedBy")),
            LastModifiedAt = reader.GetDateTime(reader.GetOrdinal("LastModifiedAt"))
        };
    }
}
