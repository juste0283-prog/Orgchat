# Sauvegarde de la base de données PostgreSQL d'OrgChat (préparation migration).
# Usage : PS> .\backup-database-postgresql.ps1 [-Database "orgchat"] [-BackupDir "...\sauvegardes"]
# Prérequis : pg_dump disponible dans le PATH et accès au serveur PostgreSQL.
# Mot de passe : définir la variable d'environnement PGPASSWORD ou utiliser un fichier .pgpass.

param(
    [string]$HostName = "localhost",
    [int]$Port = 5432,
    [string]$Database = "orgchat",
    [string]$Username = "postgres",
    [string]$BackupDir = "$PSScriptRoot\..\backups"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $BackupDir "OrgChat_$timestamp.sql"

& pg_dump -h $HostName -p $Port -U $Username -d $Database -F p -f $target
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump a échoué (code $LASTEXITCODE)."
}

Write-Host "Sauvegarde créée : $target"

# Conservation : supprime les sauvegardes de plus de 30 jours.
$limit = (Get-Date).AddDays(-30)
Get-ChildItem $BackupDir -Filter "OrgChat_*.sql" |
    Where-Object { $_.LastWriteTime -lt $limit } |
    Remove-Item -Force
