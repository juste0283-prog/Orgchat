# Sauvegarde de la base de données SQLite d'OrgChat.
# Usage : PS> .\backup-database.ps1 [-BackupDir "...\sauvegardes"]
# Reaction : copie le fichier OrgChat.db (et ses journaux WAL/SHM) vers un
# dossier de sauvegarde horodaté, avant toute mise à jour du serveur.

param(
    [string]$DatabasePath = "$PSScriptRoot\..\OrgChat.Server\OrgChat.db",
    [string]$BackupDir = "$PSScriptRoot\..\backups"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $DatabasePath)) {
    Write-Warning "Base non trouvée : $DatabasePath"
    exit 1
}

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $BackupDir "OrgChat_$timestamp.db"
Copy-Item $DatabasePath $target -Force

# Journaux SQLite (WAL/SHM) éventuels
foreach ($ext in @("-wal", "-shm")) {
    $side = "$DatabasePath$ext"
    if (Test-Path $side) {
        Copy-Item $side "$target$ext" -Force
    }
}

Write-Host "Sauvegarde créée : $target"

# Conservation : supprime les sauvegardes de plus de 30 jours.
$limit = (Get-Date).AddDays(-30)
Get-ChildItem $BackupDir -Filter "OrgChat_*.db" |
    Where-Object { $_.LastWriteTime -lt $limit } |
    Remove-Item -Force
