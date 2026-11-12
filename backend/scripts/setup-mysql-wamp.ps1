<#
.SYNOPSIS
  Script d'aide pour initialiser OrgChat avec MySQL sous WampServer (base neuve).

.DESCRIPTION
  - Crée (si possible) la base `orgchat` via le client mysql de Wamp.
  - Configure les variables d'environnement de session (secrets hors fichiers).
  - Restaure, génère la migration MySQL initiale, applique le schéma.
  - Optionnel : génère un script SQL idempotent du schéma.

  IMPORTANT: ne commitez jamais les mots de passe/clé JWT dans le dépôt.

.EXAMPLE
  # Depuis E:\OrgChat
  powershell -ExecutionPolicy Bypass -File .\backend\scripts\setup-mysql-wamp.ps1
#>

param(
  [string]$DbHost = "localhost",
  [int]$Port = 3306,
  [string]$Database = "orgchat",
  [string]$User = "root",
  [string]$Password = "",               # laisser vide si root sans mot de passe (Wamp par défaut)
  [string]$JwtKey = "",                 # si vide, une clé aléatoire est générée pour la session
  [string]$MysqlExe = "C:\wamp64\bin\mysql\mysql9.1.0\bin\mysql.exe",
  [switch]$SkipCreateDatabase,
  [switch]$GenerateSqlScript
)

$ErrorActionPreference = "Stop"

function Require-Command($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Commande requise introuvable: $name"
  }
}

Require-Command dotnet

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

# 1) Création de la base (si client mysql disponible)
if (-not $SkipCreateDatabase) {
  if (Test-Path $MysqlExe) {
    Write-Host "[1/5] Création de la base '$Database' (utf8mb4) si absente..."
    $mysqlArgs = @("-h", $DbHost, "-P", $Port, "-u", $User)
    if ($Password -ne "") { $mysqlArgs += "-p$Password" }
    $sql = "CREATE DATABASE IF NOT EXISTS \`$Database\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; SHOW DATABASES LIKE '$Database';"
    & $MysqlExe @mysqlArgs -e $sql
    if ($LASTEXITCODE -ne 0) { throw "Échec de création de la base. Vérifiez hôte/port/utilisateur/mot de passe." }
  } else {
    Write-Warning "Client mysql introuvable ($MysqlExe). Créez la base '$Database' via phpMyAdmin puis relancez avec -SkipCreateDatabase."
  }
} else {
  Write-Host "[1/5] Création de base ignorée (-SkipCreateDatabase)."
}

# 2) Secrets en variables d'environnement (session uniquement)
if ([string]::IsNullOrWhiteSpace($JwtKey)) {
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  $bytes = New-Object byte[] 48
  $rng.GetBytes($bytes)
  $JwtKey = [Convert]::ToBase64String($bytes)
  Write-Host "[2/5] Clé JWT aléatoire générée pour cette session (non affichée)."
} else {
  Write-Host "[2/5] Clé JWT fournie (session uniquement)."
}

$pwdPart = if ($Password -ne "") { "Password=$Password;" } else { "" }
$env:ConnectionStrings__DefaultConnection = "Server=$DbHost;Port=$Port;Database=$Database;User=$User;$pwdPart"
$env:JwtSettings__Key = $JwtKey

Write-Host "      ConnectionStrings__DefaultConnection définie pour $DbHost:$Port/$Database (session)."
Write-Host "      JwtSettings__Key définie (session)."

# 3) Restauration
Write-Host "[3/5] dotnet restore..."
dotnet restore .\backend\OrgChat.sln
if ($LASTEXITCODE -ne 0) { throw "dotnet restore a échoué." }

# 4) Migration MySQL initiale + application
$infra = ".\backend\OrgChat.Infrastructure"
$server = ".\backend\OrgChat.Server"
$migrationsDir = Join-Path $infra "Migrations"

if (-not (Test-Path $migrationsDir) -or -not (Get-ChildItem $migrationsDir -Filter "*InitialMySql*.cs" -ErrorAction SilentlyContinue)) {
  Write-Host "[4/5] Génération de la migration InitialMySql..."
  dotnet ef migrations add InitialMySql --project $infra --startup-project $server --output-dir Migrations
  if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations add a échoué." }
} else {
  Write-Host "[4/5] Migration InitialMySql déjà présente, étape de génération ignorée."
}

Write-Host "[5/5] Application du schéma sur MySQL (dotnet ef database update)..."
dotnet ef database update --project $infra --startup-project $server
if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update a échoué." }

if ($GenerateSqlScript) {
  $out = Join-Path $repoRoot "backend\scripts\orgchat-mysql-schema.sql"
  Write-Host "Génération du script SQL: $out"
  dotnet ef migrations script --project $infra --startup-project $server --idempotent -o $out
  if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script a échoué." }
}

Write-Host ""
Write-Host "Terminé. Vous pouvez maintenant:" -ForegroundColor Green
Write-Host "  - Ouvrir le port 5000 si nécessaire (LAN/VPN)"
Write-Host "  - Lancer: dotnet run --project .\backend\OrgChat.Server\OrgChat.Server.csproj"
Write-Host "  - Connecter les clients sur http://<IP-de-cette-machine>:5000"
Write-Host "  - Se connecter en SuperAdmin: superAdmin / superadmin123! (garanti au démarrage)"
Write-Host ""
Write-Host "Rappel: les secrets (mot de passe DB, clé JWT) ne sont définis que pour cette session PowerShell et ne sont pas écrits dans le dépôt."
