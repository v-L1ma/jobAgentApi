# Refatoração do Sistema de Scraping - Limites por Query/Plataforma

## 📋 Resumo

O sistema de scraping foi refatorado para controlar limites **por query e por plataforma**, onde apenas vagas **efetivamente adicionadas** contam para o limite. Vagas ignoradas (skipped) não afetam mais a contagem.

## 🎯 Mudanças Implementadas

### 1. Novos Arquivos Criados

#### `src/Domain/Enums/Platform.cs`
Enum para tipagem forte das plataformas de scraping:
```csharp
public enum Platform
{
    LinkedIn,
    Gupy,
    Greenhouse
}
```

#### `src/Infrastructure/Services/JobScraper/QueryExecutionCounters.cs`
Classe thread-safe para rastreamento de contadores por query + plataforma:
- Usa `ConcurrentDictionary<(Guid QueryId, Platform Platform), int>` para controle por contexto
- Métodos:
  - `TryAddJob(queryId, platform, limit)` - Tenta adicionar vaga (retorna false se atingiu limite)
  - `MarkSkipped()` - Marca vaga ignorada (não conta no limite)
  - `MarkFound()` - Marca vaga encontrada
  - `MarkSaved()` - Marca vaga salva com sucesso

### 2. Arquivos Modificados

#### `src/Infrastructure/Services/JobScraper/JobScraperOptions.cs`
Adicionadas propriedades para limites por plataforma:
```csharp
public int MaxLinkedInJobsPerQuery { get; set; } = 20;
public int MaxGupyJobsPerQuery { get; set; } = 50;
public int MaxGreenhouseJobsPerQuery { get; set; } = 50;
```

#### `src/WebApi/appsettings.json`
Novas configurações adicionadas:
```json
"JobScraper": {
  "MaxLinkedInJobsPerQuery": 20,
  "MaxGupyJobsPerQuery": 50,
  "MaxGreenhouseJobsPerQuery": 50,
  ...
}
```

#### `src/Infrastructure/Services/JobScraper/JobScraperExecutionService.cs`

**Principais alterações:**

1. **Import do namespace:**
   - Adicionado `using jobAgentApi.Domain.Enums;`

2. **Substituição de contadores:**
   - `ExecutionCounters` → `QueryExecutionCounters`
   - Contadores agora são thread-safe e por query/plataforma

3. **Método `SaveJobAsync`:**
   - Novo parâmetro: `Platform platform`
   - Verifica limite **antes** de processar a vaga:
     ```csharp
     var limit = platform switch
     {
         Platform.LinkedIn => _options.Value.MaxLinkedInJobsPerQuery,
         Platform.Gupy => _options.Value.MaxGupyJobsPerQuery,
         Platform.Greenhouse => _options.Value.MaxGreenhouseJobsPerQuery,
     };
     
     var canAdd = counters.TryAddJob(context.SearchQueryId, platform, limit);
     if (!canAdd)
     {
         _logger.LogInformation("Limite de {Limit} vagas atingido...");
         return false;
     }
     ```
   - Vagas skipped **não consomem** o limite:
     - `counters.MarkSkipped()` apenas incrementa contador de skip
     - Não chama `TryAddJob`, então não consome slot do limite

4. **Callbacks dos crawlers:**
   - Cada crawler agora passa sua plataforma explicitamente:
     ```csharp
     await RunGuypQueryWithRetryAsync(
         context,
         job => SaveJobAsync(..., Platform.Gupy, ...),
         cancellationToken);
     ```

5. **Remoção de verificação global antiga:**
   - Removido check `counters.TotalJobsSaved >= MaxJobsPerExecution` do loop principal
   - Agora o limite é verificado dentro de `SaveJobAsync` por query/plataforma
   - `MaxJobsPerExecution` ainda existe como safety net global

## 🔄 Fluxo Antes vs Depois

### ANTES
```
Execução Global
├── Limite: 20 vagas total (compartilhado entre todas queries)
├── Skipped: CONTA no limite ❌
└── Problema: Uma query podia consumir todo o limite
```

### DEPOIS
```
Query 1 (desenvolvedor)
├── LinkedIn: 0/20 vagas adicionadas
├── Gupy: 0/50 vagas adicionadas
└── Greenhouse: 0/50 vagas adicionadas

Query 2 (engenheiro)
├── LinkedIn: 0/20 vagas adicionadas
├── Gupy: 0/50 vagas adicionadas
└── Greenhouse: 0/50 vagas adicionadas

Skipped: NÃO conta no limite ✅
Cada query é independente ✅
```

## 🎯 Benefícios

1. **Justiça:** Cada query recebe sua cota justa de vagas por plataforma
2. **Eficiência:** Vagas duplicadas não desperdiçam o limite
3. **Controle Granular:** Diferentes limites por plataforma (LinkedIn=20, Gupy=50, Greenhouse=50)
4. **Thread-Safe:** `ConcurrentDictionary` previne race conditions
5. **Configurável:** Limites ajustáveis via `appsettings.json`
6. **Observabilidade:** Logs detalhados quando limites são atingidos

## 📊 Exemplo de Execução

```
[INFO] Processing query 'desenvolvedor' for user abc123
[INFO] Found 20 jobs from Greenhouse company nubank
[INFO] Job saved successfully (1/50 Gupy)
[INFO] Job skipped because it already exists
[INFO] Job skipped by excluded keyword
[INFO] Job saved successfully (2/50 Gupy)
...
[INFO] Limite de 50 vagas atingido para query 'desenvolvedor' na plataforma Gupy. Parando esta query.
[INFO] Processing next query...
```

## ⚙️ Configuração

Para ajustar os limites, edite `src/WebApi/appsettings.json`:

```json
"JobScraper": {
  "MaxLinkedInJobsPerQuery": 20,    // Vagas do LinkedIn por query
  "MaxGupyJobsPerQuery": 50,        // Vagas da Gupy por query
  "MaxGreenhouseJobsPerQuery": 50,  // Vagas do Greenhouse por query
  "MaxJobsPerExecution": 100,       // Limite global (safety net)
  "MaxApplicationsPerDay": 50       // Limite diário
}
```

## 🔒 Pontos de Atenção

- **Thread-Safety:** `ConcurrentDictionary` garante acesso concorrente seguro
- **Atomicidade:** `TryAddJob` é atômico - verifica e incrementa em uma operação
- **Semaforização:** `_executionLock` garante uma execução por vez
- **Safety Net:** `MaxJobsPerExecution` ainda protege contra execuções descontroladas

## ✅ Status

- ✅ Build: **Sucesso**
- ✅ Todos os projetos compilam sem erros
- ✅ Tests project: **Compilado**
- ✅ Sem breaking changes na API

## 📝 Próximos Passos Sugeridos

1. Monitorar execuções reais para ajustar limites
2. Adicionar métricas Prometheus para contadores por query
3. Considerar cache de preferências para evitar queries no banco
4. Testar com carga múltiplas queries simultâneas
