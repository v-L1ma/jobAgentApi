# Imagem com Playwright + .NET já configurado
FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-jammy AS base

WORKDIR /app
EXPOSE 80

# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "jobAgentApi.WebApi.dll"]