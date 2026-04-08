# 🕷️ Plano de Implementação - Crawlers (Gupy e Greenhouse)

## 🎯 Objetivo

Implementar crawlers robustos, escaláveis e seguros para coletar vagas de emprego das plataformas **Gupy** e **Greenhouse**, sem necessidade de autenticação, integrando com a API .NET existente.

---

# 🧠 Visão Geral da Estratégia

* Executar crawlers via **BackgroundService (.NET)**
* Rodar em intervalos controlados (30–60 min com randomização)
* Evitar bloqueios (delay + limitação de páginas)
* Salvar vagas no PostgreSQL
* Evitar duplicação

---

# 📦 Estrutura Recomendada

## Interface

```csharp
public interface ICrawlerService
{
    Task<List<Vacancy>> CrawlAsync(SearchProfile profile);
}
```

---

# 🟢 CRAWLER GUPY

## 🔎 Como funciona

A Gupy usa páginas públicas de carreira no formato:

```
https://nome-da-empresa.gupy.io
```

Ou listagens diretas:

```
https://portal.gupy.io/job-search/term=react
```

---

## ⚙️ Fluxo de Implementação

### 1. Montar URL de busca

```csharp
var url = $"https://portal.gupy.io/job-search/term={keyword}";
```

---

### 2. Abrir página com Playwright

```csharp
var page = await browser.NewPageAsync();
await page.GotoAsync(url);
```

---

### 3. Esperar carregamento

```csharp
await page.WaitForSelectorAsync(".sc-1peq8zo-0");
```

---

### 4. Extrair vagas

* título
* empresa
* localização
* link

```csharp
var jobs = await page.QuerySelectorAllAsync(".job-card");
```

---

### 5. Entrar na vaga (opcional)

Extrair descrição completa:

```csharp
await page.GotoAsync(jobUrl);
```

---

### 6. Paginação

* máximo: 3 páginas

```csharp
await page.ClickAsync("button.next-page");
```

---

### 7. Delay anti-bot

```csharp
await Task.Delay(Random.Shared.Next(2000, 5000));
```

---

## ⚠️ Cuidados

* limitar requisições
* evitar loops infinitos
* tratar timeout

---

# 🟢 CRAWLER GREENHOUSE

## 🔎 Como funciona

Greenhouse possui endpoints JSON públicos:

```
https://boards-api.greenhouse.io/v1/boards/{company}/jobs
```

👉 Isso é MUITO melhor que scraping HTML

---

## ⚙️ Fluxo de Implementação

### 1. Lista de empresas

Você precisa manter uma lista:

```csharp
var companies = new[] { "nubank", "ifood", "stripe" };
```

---

### 2. Fazer request HTTP

```csharp
var response = await httpClient.GetAsync(url);
```

---

### 3. Parse JSON

```csharp
var content = await response.Content.ReadAsStringAsync();
var data = JsonSerializer.Deserialize<GreenhouseResponse>(content);
```

---

### 4. Mapear vagas

Campos úteis:

* title
* location
* absolute_url
* updated_at

---

### 5. Filtrar por keyword

```csharp
if (job.Title.Contains(keyword))
```

---

## ⚠️ Vantagens

✔ rápido
✔ sem bloqueio
✔ sem Playwright

---

# 🧱 MODELO DE DADOS (Vacancy)

```csharp
public class Vacancy
{
    public string Title { get; set; }
    public string Company { get; set; }
    public string Location { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
    public string Platform { get; set; }
}
```

---

# 🔁 DEDUPLICAÇÃO

Evitar vagas duplicadas:

## Estratégia

* usar URL como chave única

```csharp
if (!db.Vacancies.Any(v => v.Url == job.Url))
```

---

# ⏱️ AGENDAMENTO

```csharp
while (true)
{
    await Crawl();
    await Task.Delay(TimeSpan.FromMinutes(Random(30, 60)));
}
```

---

# 🚀 MELHORIAS FUTURAS

* score de match
* ranking por relevância
* notificação de novas vagas
* cache de resultados

---

# 🧭 RESUMO

## Gupy

* usa Playwright
* scraping HTML
* mais complexo

## Greenhouse

* usa API pública
* rápido e estável
* prioridade alta

---

# ✅ PRIORIDADE DE IMPLEMENTAÇÃO

1. Greenhouse (rápido e fácil)
2. Gupy (mais trabalhoso)

---

## 🎯 Resultado esperado

* base de vagas populada
* crawler rodando em background
* integração com Preferences
* sistema pronto para match inteligente
