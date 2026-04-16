# Job Search Async Scraping MVP

Sistema de busca de vagas com scraping assíncrono, cache e polling para evitar bloqueio de requisições HTTP.

## Visão Geral

Este sistema permite que o usuário busque vagas sem esperar o scraping terminar, retornando dados progressivamente através de polling.

## Fluxo de Funcionamento

### 1. Usuário realiza uma busca
```
GET /api/jobs/search?query=desenvolvedor+frontend
```

### 2. Backend verifica cache
- Se existe cache válido → retorna dados imediatamente com `isLoading: false`
- Se não existe cache → enfileira scraping e retorna resposta parcial com `isLoading: true`

### 3. Frontend inicia polling
Quando recebe `isLoading: true`, o frontend deve fazer requisições repetidas a cada 2-5 segundos até receber `isLoading: false`.

### 4. BackgroundService processa a fila
Um serviço em background consome a fila de queries, executa o scraping e salva os resultados no cache.

### 5. Próximas requisições
Nas próximas requisições do polling, os dados já estarão disponíveis no cache e serão retornados imediatamente.

## Endpoints

### GET /api/jobs/search

Endpoint assíncrono com cache e polling.

**Parâmetros:**
- `query` (string, opcional): Termo de busca (ex: "desenvolvedor frontend")
- `stack` (string, opcional): Stack tecnológica
- `location` (string, opcional): Localização
- `page` (int, opcional, padrão: 1): Número da página
- `pageSize` (int, opcional, padrão: 10): Itens por página

**Resposta Parcial (quando scraping está em andamento):**
```json
{
  "status": "partial",
  "isLoading": true,
  "data": [],
  "meta": {
    "scraperRunning": true,
    "fromCache": false,
    "requestId": "guid-aqui"
  }
}
```

**Resposta Completa (quando dados estão disponíveis):**
```json
{
  "status": "complete",
  "isLoading": false,
  "data": [
    {
      "id": "guid",
      "title": "Frontend Developer",
      "description": "Descrição da vaga...",
      "url": "https://...",
      "isApplied": false,
      "company": "Empresa X",
      "location": "São Paulo",
      "platform": "LinkedIn"
    }
  ],
  "meta": {
    "scraperRunning": false,
    "fromCache": true,
    "totalItems": 10,
    "currentPage": 1,
    "totalPages": 1
  }
}
```

## Exemplo de Uso no Frontend

```javascript
async function searchJobs(query) {
  let isLoading = true;
  let maxAttempts = 30; // Timeout após 30 tentativas
  let attempts = 0;
  
  while (isLoading && attempts < maxAttempts) {
    const response = await fetch(
      `/api/jobs/search?query=${encodeURIComponent(query)}`,
      {
        headers: { 'Authorization': `Bearer ${token}` }
      }
    );
    
    const data = await response.json();
    
    if (data.isLoading) {
      // Scraping ainda está em andamento
      console.log('Scraping em progresso, tentando novamente em 3 segundos...');
      await new Promise(resolve => setTimeout(resolve, 3000));
      attempts++;
    } else {
      // Dados disponíveis
      console.log('Dados recebidos:', data.data);
      isLoading = false;
      return data.data;
    }
  }
  
  if (attempts >= maxAttempts) {
    console.error('Timeout: scraping demorou muito tempo');
  }
}
```

## Componentes Implementados

### 1. JobCacheService
- **Localização:** `src/Infrastructure/Services/Cache/JobCacheService.cs`
- **Responsabilidade:** Gerenciar cache de resultados de busca
- **Tipo:** MemoryCache
- **TTL:** 15 minutos (configurável)
- **Key Pattern:** `job_search:{query_normalizada}`

### 2. JobScrapingQueueService
- **Localização:** `src/Infrastructure/Services/JobScraperQueue/JobScrapingQueueService.cs`
- **Responsabilidade:** Gerenciar fila de scraping
- **Tipo:** Channel<T> (fila em memória)
- **Funcionalidades:**
  - Evita scraping duplicado para mesma query
  - Tracking de queries em execução
  - Controle de concorrência

### 3. JobScrapingQueueBackgroundService
- **Localização:** `src/Infrastructure/Services/JobScraperQueue/JobScrapingQueueBackgroundService.cs`
- **Responsabilidade:** Consumir fila de scraping e executar scrapers
- **Funcionalidades:**
  - Consome queries da fila
  - Executa scraping para cada query
  - Salva resultados no cache

### 4. GetJobsAsyncQueryHandler
- **Localização:** `src/Application/Features/Jobs/Queries/GetJobsAsync/`
- **Responsabilidade:** Handler CQRS para busca assíncrona
- **Lógica:**
  1. Verifica cache primeiro
  2. Se não existe cache, verifica se scraping já está em execução
  3. Se não está em execução, enfileira scraping
  4. Retorna resposta adequada (parcial ou completa)

## Configuração

### appsettings.json

```json
{
  "JobCache": {
    "TtlMinutes": 15,
    "MaxCacheSize": 100
  },
  "JobScraper": {
    "Enabled": true,
    "IntervalMinutes": 30,
    "Headless": true,
    "MaxJobsPerExecution": 100,
    "MaxApplicationsPerDay": 50
  }
}
```

## Otimizações Implementadas

1. ✅ **Normalização de query** (lowercase + trim)
2. ✅ **Evitar scraping duplicado** para mesma query simultânea (ConcurrentDictionary)
3. ✅ **Controle de TTL do cache** (15 minutos configurável)
4. ✅ **Limitar frequência de scraping** por query (cache TTL)
5. ✅ **Request ID para rastreamento** (Guid por requisição)

## Problemas Resolvidos

| Problema | Solução |
|----------|---------|
| Scraping duplicado para mesma query | ConcurrentDictionary tracking queries em execução |
| Resposta lenta | Retorna cache imediatamente + background scraping |
| Alta carga no scraper | Fila com Channel<T> e controle de concorrência |

## Melhorias Futuras

- [ ] Substituir MemoryCache por Redis
- [ ] Substituir fila em memória por RabbitMQ
- [ ] Implementar WebSocket ao invés de polling
- [ ] Persistir dados no banco
- [ ] Ranking e deduplicação de vagas
- [ ] Cache mais granular (por query + filtros)

## Arquitetura

```
WebApi (Controllers)
  ↓
Application (CQRS Handlers)
  ↓
Infrastructure (Services + Repositories)
  ↓
Domain (Entities + Enums)
```

### Fluxo de Dependências
```
GET /api/jobs/search
  → JobsController.GetJobsAsync()
    → MediatR.Send(GetJobsAsyncQuery)
      → GetJobsAsyncQueryHandler
        → IJobCacheService.GetCachedResultAsync()
          → Cache HIT → Retorna dados
          → Cache MISS → IJobScrapingQueueService.IsQueryInProgress()
            → Em execução → Retorna partial
            → Não em execução → IJobScrapingQueueService.EnqueueScrapingRequestAsync()
              → Retorna partial
                → JobScrapingQueueBackgroundService consome fila
                  → Executa scraping
                    → Salva no cache
                      → Próxima requisição recebe dados do cache
```

## Testes

Para testar o sistema:

1. Inicie a API
2. Faça uma requisição para `GET /api/jobs/search?query=teste`
3. Se receber `isLoading: true`, faça polling a cada 3 segundos
4. Quando receber `isLoading: false`, os dados estão disponíveis
5. Faça outra requisição para a mesma query - deve retornar dados imediatamente (cache)

## Notas

- O sistema mantém o endpoint síncrono original (`GET /api/jobs`) para compatibilidade
- O novo endpoint assíncrono (`GET /api/jobs/search`) é recomendado para melhor UX
- BackgroundService executa automaticamente quando a aplicação inicia
- Cache é armazenado em memória (MVP) - para produção, considerar Redis
