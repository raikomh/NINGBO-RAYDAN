# ── Stage 1: Build ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los archivos de proyecto primero para cachear la restauración de paquetes
COPY ["BusinessSearcher.Domain/BusinessSearcher.Domain.csproj",         "BusinessSearcher.Domain/"]
COPY ["BusinessSearcher.Application/BusinessSearcher.Application.csproj","BusinessSearcher.Application/"]
COPY ["BusinessSearcher.Infrastructure/BusinessSearcher.Infrastructure.csproj","BusinessSearcher.Infrastructure/"]
COPY ["BusinessSearcher.WebApi/BusinessSearcher.WebApi.csproj",         "BusinessSearcher.WebApi/"]

RUN dotnet restore "BusinessSearcher.WebApi/BusinessSearcher.WebApi.csproj"

# Copia el resto del código
COPY . .

# Build en Release
RUN dotnet build "BusinessSearcher.WebApi/BusinessSearcher.WebApi.csproj" \
    -c Release -o /app/build

# ── Stage 2: Publish ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "BusinessSearcher.WebApi/BusinessSearcher.WebApi.csproj" \
    -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Stage 3: Runtime (imagen final ligera) ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Usuario no-root por seguridad
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Directorio para logs
RUN mkdir -p /app/logs && chown -R appuser:appgroup /app

COPY --from=publish /app/publish .

USER appuser

EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "BusinessSearcher.WebApi.dll"]
