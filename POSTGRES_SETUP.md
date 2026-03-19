# Setup PostgreSQL - jobAgentApi

## ✅ Configurações Já Realizadas

1. **AppDbContext.cs** - Configurado com todos os DbSets:
   - User
   - JobApplication
   - Plataform
   - PlataformConfiguration
   - Preference
   - Questoes

2. **appsettings.json** - Connection string adicionada:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=5432;Database=jobagentapi;User Id=postgres;Password=postgres;"
   }
   ```

3. **appsettings.Development.json** - Configurado para desenvolvimento com logs do EF Core

4. **docker-compose.yml** - PostgreSQL 16 adicionado com:
   - Container: jobagentapi_postgres
   - Usuário: postgres
   - Senha: postgres
   - Database: jobagentapi
   - Health check configurado
   - Volume persistente: postgres_data

5. **jobAgentApi.Infrastructure.csproj** - Pacotes NuGet já inclusos:
   - Microsoft.EntityFrameworkCore (8.0.0)
   - Npgsql.EntityFrameworkCore.PostgreSQL (8.0.0)

## 🚀 Próximos Passos

### Opção 1: Usar Docker Compose (Recomendado)

```bash
# Inicie todos os serviços (PostgreSQL, API, Prometheus, Grafana)
docker-compose up -d

# Verifique se o PostgreSQL está rodando
docker-compose ps
```

A API estará disponível em: `http://localhost:5000`
PostgreSQL estará em: `localhost:5432`

### Opção 2: PostgreSQL Local

Se você preferir não usar Docker:

1. Instale o PostgreSQL 16 localmente
2. Crie um database chamado `jobagentapi`
3. Deixe a connection string em `appsettings.json` como está

## 📦 Criar e Aplicar Migrations

### Instalar EF Core CLI (uma vez)

```bash
dotnet tool install --global dotnet-ef
```

### Criar Initial Migration

Navegue até a pasta do projeto WebApi:

```bash
cd src/WebApi

# Criar a migration inicial
dotnet ef migrations add InitialCreate --project ../Infrastructure/jobAgentApi.Infrastructure.csproj --startup-project jobAgentApi.WebApi.csproj

# Atualizar o banco de dados
dotnet ef database update --project ../Infrastructure/jobAgentApi.Infrastructure.csproj --startup-project jobAgentApi.WebApi.csproj
```

### Criar Outras Migrations

Se adicionar novas entidades ou modificar as existentes:

```bash
dotnet ef migrations add NomeDaMigration --project ../Infrastructure/jobAgentApi.Infrastructure.csproj --startup-project jobAgentApi.WebApi.csproj

dotnet ef database update --project ../Infrastructure/jobAgentApi.Infrastructure.csproj --startup-project jobAgentApi.WebApi.csproj
```

## 🔍 Verificar Conexão

```bash
# Via psql (se tiver PostgreSQL instalado localmente)
psql -h localhost -U postgres -d jobagentapi

# Via Docker
docker exec -it jobagentapi_postgres psql -U postgres -d jobagentapi
```

## 📝 Exemplo de Query

```bash
# Listar tabelas criadas
docker exec -it jobagentapi_postgres psql -U postgres -d jobagentapi -c "\\dt"
```

## ⚙️ Variáveis de Ambiente (Opcional)

Se quiser usar variáveis de ambiente em vez de appsettings.json:

```bash
# Linux/Mac
export ConnectionStrings__DefaultConnection="Server=localhost;Port=5432;Database=jobagentapi;User Id=postgres;Password=postgres;"

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Port=5432;Database=jobagentapi;User Id=postgres;Password=postgres;"
```

## 🟡 Observações Importantes

1. A senha padrão é `postgres` - **mude em produção!**
2. O banco de dados está configurado para ser persistente com volume Docker
3. Se adicionar novas entidades, atualize `AppDbContext.cs` com os respectivos DbSets
4. Para arrays (como em Preference), use conversões explícitas no `OnModelCreating`

## 🆘 Troubleshooting

### Erro: "Unable to connect to database"
- Verifique se o PostgreSQL está rodando: `docker-compose ps`
- Confirme a connection string em `appsettings.json`
- Teste a conexão: `docker exec -it jobagentapi_postgres pg_isready -h localhost`

### Erro: "migrations already applied"
- Delete as migrations se elas forem incorretas
- Sempre teste em ambiente de desenvolvimento primeiro

### Limpar o banco de dados

```bash
# Remove o volume e recria
docker-compose down -v
docker-compose up -d
```

---

**Data:** 19 de março de 2026
**Versão:** PostgreSQL 16 (Alpine)
**Framework:** .NET 8.0
