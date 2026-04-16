# Implementação de Novos Crawlers - Gupy e Greenhouse

## 🎯 Resumo da Implementação

Foram implementados com sucesso **2 novos crawlers** para plataformas de vagas, seguindo o padrão do LinkedIn existente:

---

## 📦 Arquivos Criados

### 1. **PlaywrightBrowserManager.cs**
- **Localização**: `src/Infrastructure/Services/JobScraper/PlaywrightBrowserManager.cs`
- **Propósito**: Gerenciar uma única instância compartilhada do navegador Playwright para evitar problemas de performance
- **Funcionalidade**:
  - Singleton que mantém uma única instância do browser
  - Reutiliza contextos de navegador para múltiplos scrapers
  - Gerenciamento seguro com lock sincronizado
  - Lança o navegador com opções de segurança (`--no-sandbox`, `--disable-setuid-sandbox`)

### 2. **GuypJobScraper.cs**
- **Localização**: `src/Infrastructure/Services/JobScraper/GuypJobScraper.cs`
- **Propósito**: Coletar vagas do portal Gupy via HTML scraping
- **Características**:
  - Usa Playwright via BrowserManager compartilhado
  - Suporta paginação (máx. 3 páginas)
  - Filtro por localização
  - Deduplicação por URL
  - Delays aleatórios (anti-bot)
  - Inicia contexto de browser: `gupy-context`

### 3. **GreenhouseJobScraper.cs**
- **Localização**: `src/Infrastructure/Services/JobScraper/GreenhouseJobScraper.cs`
- **Propósito**: Coletar vagas de empresas Greenhouse via API JSON pública
- **Características**:
  - **Sem Playwright** - usa HttpClient para chamadas de API
  - API pública: `https://boards-api.greenhouse.io/v1/boards/{company}/jobs`
  - Suporta lista de empresas conhecidas (Nubank, iFood, Stripe, etc)
  - Filtro por query e localização
  - Muito mais rápido e estável que HTML scraping
  - Trata erros de HTTP com retry

---

## 📋 Arquivos Modificados

### 1. **JobScraperExecutionService.cs**
- **Alterações**:
  - Adicionado suporte para 3 scrapers: LinkedIn, Gupy, Greenhouse
  - Novo loop de execução que chama todos os 3 sequencialmente
  - Métodos de retry para cada scraper: `RunLinkedInQueryWithRetryAsync`, `RunGuypQueryWithRetryAsync`, `RunGreenhouseQueryWithRetryAsync`
  - Lógica de salvamento unificada em `SaveJobAsync`
  - Contadores compartilhados via classes `QueryState` e `ExecutionCounters`
  - Suporte a deduplicação por `PlataformJobId` ou URL

### 2. **DependencyInjection.cs**
- **Alterações**:
  - Registrado `IPlaywrightBrowserManager` como Singleton
  - Registrado `IGuypJobScraper` como Singleton
  - Registrado `IGreenhouseJobScraper` como Singleton
  - Todos como dependências injecionáveis

---

## 🔄 Fluxo de Execução

```
JobScraperBackgroundService (agendado a cada 30-60 min)
    ↓
JobScraperExecutionService.ExecuteAsync()
    ↓
Para cada UserSearchQuery ativa:
    ├─ RunLinkedInQueryWithRetryAsync()
    │   └─ LinkedInJobScraper (cria próprio browser)
    │
    ├─ RunGuypQueryWithRetryAsync()
    │   └─ GuypJobScraper (usa BrowserManager compartilhado)
    │
    └─ RunGreenhouseQueryWithRetryAsync()
        └─ GreenhouseJobScraper (usa HttpClient)
        
    Para cada job encontrado:
        └─ SaveJobAsync() → Verifica duplicação → Aplica filtros → Salva BD
```

---

## 🚀 Performance & Otimizações

### Navegador Compartilhado
- ✅ **PlaywrightBrowserManager**: Uma única instância do navigador para Gupy
- ✅ Reutiliza contextos de browser
- ✅ Evita múltiplas inicializações (problema de performance)

### Greenhouse - Sem Playwright
- ✅ API JSON pública = **muito mais rápido**
- ✅ Sem overhead de automação de browser
- ✅ Melhor para larga escala

### Deduplicação
- ✅ Verifica por `PlataformJobId` (ID único da plataforma)
- ✅ Verifica por `Url` como fallback
- ✅ Evita duplicatas no BD

---

## 🧪 Compilação

```bash
dotnet build --no-restore
# ✅ Sucesso - 0 erros, 0 avisos
```

---

## 📝 Próximas Melhorias

1. **Score de Match**: Integrar com `SearchQueryMatcherService` para ranking por relevância
2. **Cache de Resultados**: Evitar re-processamento frequente
3. **Notificações**: Alertar usuários sobre novas vagas relevantes
4. **Pool de Empresas Greenhouse**: Permitir configuração dinâmica
5. **Monitoramento**: Métricas de sucesso/falha por scraper

---

## 🛠️ Padrão Seguido

Toda implementação seguiu o **padrão do LinkedIn**:
- Interfaces segregadas por scraper
- Métodos `StreamJobsAsync` com callbacks
- Suporte a retry com backoff exponencial
- Logging estruturado
- Tratamento de exceções específicas

---

**Status**: ✅ Implementação completa e compilação bem-sucedida
