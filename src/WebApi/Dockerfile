# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-jammy AS final

WORKDIR /app
EXPOSE 80

# Ensure browsers are available and PATH is set for running Playwright
ENV PATH="/root/.dotnet/tools:${PATH}"
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=0
ENV DOTNET_TieredCompilation=1

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "jobAgentApi.WebApi.dll"]