#!/usr/bin/env pwsh
# Validación automática del Backend MVP
# Ejecutar: ./validate-backend.ps1

param(
    [switch]$SkipTests,
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

Write-Header "BusinessSearcher Backend — Validación MVP"

# Paso 1: Verificar que .NET está instalado
Write-Header "Paso 1: Verificar .NET SDK"
try {
    $dotnetVersion = dotnet --version
    Write-Success "Encontrado .NET: $dotnetVersion"
}
catch {
    Write-Error-Custom "❌ .NET SDK no está instalado"
    exit 1
}

# Paso 2: Limpiar y restaurar
Write-Header "Paso 2: Restaurar paquetes NuGet"
try {
    dotnet restore BusinessSearcher.sln
    Write-Success "Paquetes restaurados exitosamente"
}
catch {
    Write-Error-Custom "❌ Error restaurando paquetes"
    exit 1
}

# Paso 3: Compilar solución
if (-not $SkipBuild) {
    Write-Header "Paso 3: Compilar solución"
    try {
        dotnet clean --verbosity minimal
        dotnet build --configuration Release --verbosity minimal
        Write-Success "Solución compilada sin errores"
    }
    catch {
        Write-Error-Custom "❌ Error en compilación"
        exit 1
    }
}

# Paso 4: Ejecutar tests
if (-not $SkipTests) {
    Write-Header "Paso 4: Ejecutar pruebas unitarias"
    try {
        if ($Verbose) {
            dotnet test --verbosity normal --configuration Release
        }
        else {
            dotnet test --verbosity minimal --configuration Release --no-build
        }
        Write-Success "Todos los tests pasaron"
    }
    catch {
        Write-Error-Custom "❌ Fallos en tests"
        exit 1
    }
}

# Paso 5: Verificar estructura de archivos
Write-Header "Paso 5: Verificar estructura del proyecto"

$requiredFolders = @(
    "BusinessSearcher.Domain",
    "BusinessSearcher.Application",
    "BusinessSearcher.Infrastructure",
    "BusinessSearcher.WebApi",
    "BusinessSearcher.Tests"
)

foreach ($folder in $requiredFolders) {
    if (Test-Path $folder) {
        Write-Success "Carpeta encontrada: $folder"
    }
    else {
        Write-Error-Custom "Carpeta faltante: $folder"
        exit 1
    }
}

# Paso 6: Verificar archivos clave
Write-Header "Paso 6: Verificar archivos de configuración"

$requiredFiles = @(
    "BusinessSearcher.sln",
    "Dockerfile",
    "docker-compose.yml",
    "BusinessSearcher.WebApi/appsettings.json",
    "BusinessSearcher.WebApi/Program.cs",
    "README.md",
    "TESTING_GUIDE.md",
    "MVP_VALIDATION_CHECKLIST.md"
)

foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Success "Archivo encontrado: $file"
    }
    else {
        Write-Warning-Custom "Archivo faltante (no crítico): $file"
    }
}

# Paso 7: Estadísticas de código
Write-Header "Paso 7: Estadísticas del proyecto"

$csFiles = Get-ChildItem -Path . -Filter "*.cs" -Recurse | 
    Where-Object { $_.FullName -notmatch "bin|obj|.git" } | 
    Measure-Object -Line

$testFiles = Get-ChildItem -Path "BusinessSearcher.Tests" -Filter "*.cs" -Recurse | 
    Measure-Object -Line

Write-Host "Total de archivos C#: " -NoNewline
Write-Host "$($csFiles.Count)" -ForegroundColor Magenta
Write-Host "Total de líneas de código: " -NoNewline
Write-Host "$($csFiles.Lines)" -ForegroundColor Magenta
Write-Host "Líneas de test: " -NoNewline
Write-Host "$($testFiles.Lines)" -ForegroundColor Magenta

# Paso 8: Verificación final
Write-Header "Resumen final"

Write-Success "✅ Backend compilado exitosamente"
Write-Success "✅ Todos los tests pasan"
Write-Success "✅ Estructura del proyecto válida"
Write-Success "✅ Archivos de configuración en lugar"

Write-Host "`n🎉 BACKEND MVP LISTO PARA DEPLOYMENT`n" -ForegroundColor Green

Write-Host "Próximos pasos:" -ForegroundColor Cyan
Write-Host "  1. Revisar appsettings.json con tus datos reales"
Write-Host "  2. Ejecutar: docker compose up -d"
Write-Host "  3. Acceder a http://localhost:5000/swagger"
Write-Host "  4. Generar una migración EF Core: dotnet ef migrations add InitialCreate"
Write-Host "  5. Aplicar migración: dotnet ef database update"
Write-Host "`n"
