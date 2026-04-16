# 💼 Job Agent API - Centralizador de Vagas & CV Inteligente

## 🎯 Visão Geral
O **Job Agent API** é o backend central de uma plataforma inovadora desenvolvida em **.NET 8**, projetada para revolucionar a busca por emprego. O sistema atua como um hub inteligente que centraliza vagas de diversas plataformas (como LinkedIn e Gupy), realiza análise de compatibilidade (Match) baseada em IA e auxilia na otimização de currículos para aumentar as chances de aprovação dos candidatos.

## 🚀 Principais Funcionalidades
- **Centralização de Vagas**: Integração com crawlers externos para consolidar vagas de múltiplas fontes.
- **Match Inteligente**: Análise automatizada entre o perfil do candidato (Node.js, C#, etc.) e os requisitos da vaga.
- **Gestão de Currículos (CV)**: Armazenamento e geração de currículos personalizados otimizados para cada oportunidade.
- **Preferências Personalizadas**: Configuração detalhada de senioridade, pretensão salarial e stack tecnológica.
- **Simulação de Entrevistas**: Banco de questões e suporte de IA para preparação técnica.
- **Monitoramento e Métricas**: Suporte nativo ao Prometheus para acompanhamento de performance e saúde da API.

## 🏗 Arquitetura e Tecnologias
O projeto segue os princípios da **Clean Architecture**, garantindo alta testabilidade, baixo acoplamento e independência de frameworks externos.

### Stack Tecnológica
- **Linguagem**: C# 12
- **Framework**: .NET 8 (ASP.NET Core Web API)
- **Banco de Dados**: PostgreSQL (Entity Framework Core)
- **Mensageria/Padronização**: MediatR (CQRS), FluentValidation
- **Containerização**: Docker e Docker Compose
- **Observabilidade**: Prometheus
- **Testes**: xUnit, NSubstitute, FluentAssertions

### Camadas da Solução
- **Domain**: Entidades de negócio (`Job`, `ApplicationUser`, `UserCv`), Enums e exceções de domínio.
- **Application**: Casos de uso divididos por features, Commands/Queries e Behaviors (Logging, Validation, Performance).
- **Infrastructure**: Implementação de Repositórios, Contexto do Banco de Dados, Migrations e Serviços Externos.
- **WebApi**: Controladores REST, configurações de DI e Middleware.

## 📁 Estrutura do Projeto
```text
jobAgentApi/
├── src/
│   ├── Domain/          # Regras de negócio e entidades core
│   ├── Application/     # Casos de uso (Mediator/CQRS)
│   ├── Infrastructure/  # Acesso a dados e serviços externos
│   └── WebApi/          # Entry point da aplicação (Endpoints)
├── tests/
│   └── jobAgentApi.Tests/ # Testes unitários e de integração
├── docker-compose.yml   # Orquestração de infraestrutura (DB, Prometheus)
└── prometheus.yml       # Configurações de monitoramento
```

## 🛠 Como Executar
1. **Infraestrutura**:
   ```bash
   docker-compose up -d
   ```
2. **Executar API**:
   ```bash
   cd src/WebApi
   dotnet run
   ```
3. **Documentação**: Acesse `/swagger` para visualizar os endpoints disponíveis via Swagger UI.

---
Desenvolvido com foco em escalabilidade e eficiência para o ecossistema de recrutamento.
