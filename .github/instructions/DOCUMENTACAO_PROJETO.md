# 💼 Job Agent API - Centralizador de Vagas & CV Inteligente

## 🎯 Visão Geral do Projeto
O **Job Agent API** é o backend central da plataforma, desenvolvido em .NET 8 focado em ajudar candidatos a encontrarem vagas relevantes e otimizarem seus currículos. O sistema atua como o motor para integração com crawlers de vagas externos, análise inteligente de compatibilidade (Match) e armazenamento dos perfis e candidaturas.

---

## 🏗 Estrutura e Arquitetura (Clean Architecture)

O projeto foi construído seguindo os princípios da **Clean Architecture** para garantir isolamento de regras de negócios, testabilidade e separação de responsabilidades.

- **Domain (`src/Domain`)**: O coração do software. Contém as entidades de negócio como `ApplicationUser`, `Vacancies`, `Plataform`, `Preference`, `Question` e `Roles`, além de validações e exceções de domínio (`DomainExeception`).
- **Application (`src/Application`)**: Orquestra os casos de uso do sistema. Utiliza separação por **Features** (`Auth`, `Vacancies`, `Plataform`, `Preferences`, `User`) e aplica Padrões de Mediator/CQRS com behaviors (Tratamento de Exceções, Logging, Performance e Validação).
- **Infrastructure (`src/Infrastructure`)**: Lida com detalhes técnicos como acesso a banco de dados (EF Core com `AppDbContext`), implementações do padrão `UnitOfWork` e Repositórios (`UserRepository`), além de serviços de infraestrutura gerais.
- **WebApi (`src/WebApi`)**: A camada de apresentação (REST API). Fornece os endpoints de acesso, configurações de contêineres e integração com documentações via Swagger.

---

## 🚀 Módulos e Funcionalidades Atuais

Baseado na visão do MVP e na atual infraestrutura do código:

1. **Gestão de Usuários e Autenticação (`Auth` / `User`)**
   - Criação e manutenção de credenciais do candidato (`ApplicationUser`).
   - Gestão de regras e papéis (`Roles`).

2. **Preferências de Candidatura (`Preferences`)**
   - Mapeamento do perfil do usuário: senioridade, Stack Tecnológica, salários e localidade, usados para o Match Inteligente.

3. **Gerenciamento de Plataformas (`Plataform` / `PlataformConfiguration`)**
   - Gestão das fontes de vagas (por exemplo, LinkedIn, Gupy, etc.) utilizadas pelo Crawler para buscar e alimentar a base de dados do sistema.

4. **Vizualização de Vagas (`Vacancies`)**
   - Histórico e acompanhamento das candidaturas (criação de currículo personalizado e envio).

5. **Perguntas e Entrevistas (`Question`)**
   - Banco de questões e suporte de IA para extração de requisitos ou preparações de entrevista para a vaga avaliada.

---

## 🛠 Stack Tecnológica e Ferramental

- **Backend**: C# 12 / .NET 8 (ASP.NET Core Web API)
- **Banco de Dados**: PostgreSQL (Instruções em `POSTGRES_SETUP.md`)
- **Padrões Arquiteturais**: Clean Architecture, CQRS, Repository & Unit of Work, MediatR
- **Containerização**: Docker e Docker Compose (`Dockerfile`, `docker-compose.yml`)
- **Monitoramento e Métricas**: Prometheus (`prometheus.yml`)
- **Testes**: xUnit / NSubstitute / FluentAssertions (no projeto `jobAgentApi.Tests`)

---

## 🔌 Integrações Previstas na Arquitetura

Conforme desenhado no modelo de produto MVPs:
- **Agentes Crawlers**: Consomem configurações do módulo de `Plataform` para injetar vagas de forma estruturada.
- **Microserviços Analíticos de IA**: Comunicação inter-serviços que recebe a vaga + CV do usuário para estruturar **Gaps**, **Match de Skills** e **Geração do CV final em PDF**.

---

## 🚀 Próximos Passos & Setup Local

1. Suba o banco de dados e monitoramento usando Docker:
   ```bash
   docker-compose up -d
   ```
2. Consulte o arquivo `POSTGRES_SETUP.md` para garantir credenciais corretas que refletem o `appsettings.Development.json`.
3. Para iniciar a API:
   ```bash
   cd src/WebApi
   dotnet run
   ```