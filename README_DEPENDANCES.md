ORGCHAT – DOSSIER DOCUMENTAIRE
================================

Fichiers :
1. 01_Architecture_OrgChat.docx
2. 02_Cahier_des_charges_OrgChat.docx
3. 03_Roadmap_OrgChat.docx

CHOIX TECHNIQUES
- Client : C# Windows Forms, .NET 10 (net10.0-windows)
- Serveur : C# ASP.NET Core Web API
- Temps réel : ASP.NET Core SignalR
- ORM : Entity Framework Core
- Base (actuelle) : SQLite, fichier détenu exclusivement par le serveur
- Préparation migration : PostgreSQL (provider Npgsql référencé, bascule via `DatabaseProvider` ; migrations PostgreSQL à générer lors de la migration)
- IDE : Visual Studio 2026 (charges .NET desktop + ASP.NET/web)
- Dépôt : Git/GitHub

PACKAGES PRINCIPAUX

Frontend (dans OrgChat.Client) :
dotnet add package Microsoft.AspNetCore.SignalR.Client
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Http
# Optionnel :
dotnet add package CommunityToolkit.Mvvm

Backend / Infrastructure :
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
# SignalR serveur est inclus dans ASP.NET Core ; pas de package SignalR serveur séparé requis.

Outil EF :
dotnet tool install --global dotnet-ef

Tests (dans OrgChat.Tests) :
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
# Optionnels :
dotnet add package Moq
dotnet add package Microsoft.AspNetCore.Mvc.Testing

OUTILS À INSTALLER
- .NET 10 SDK : https://dotnet.microsoft.com/download/dotnet/10.0
- Visual Studio : https://visualstudio.microsoft.com/
- Git : https://git-scm.com/downloads
- DB Browser for SQLite : https://sqlitebrowser.org/
- Insomnia : https://insomnia.rest/

IMPORTANT SQLITE / PRÉPARATION POSTGRESQL
SQLite (actuel) est une base embarquée, pas un serveur. Ne placez pas OrgChat.db sur un dossier partagé pour que les postes clients l'ouvrent directement. Le backend doit être le seul à accéder au fichier. Les clients passent par l'API et SignalR. 
Le projet est préparé pour une migration future vers PostgreSQL (package Npgsql inclus, `DatabaseProvider`, chaîne `ConnectionStrings:PostgreSQL`, script `backend/scripts/backup-database-postgresql.ps1`) afin de mieux supporter les accès concurrents et les déploiements distribués ; la bascule réelle nécessite des migrations EF Core PostgreSQL et, si besoin, une migration des données.

COMMANDES DE CONTRÔLE
dotnet --info
dotnet --list-sdks
dotnet restore
dotnet build
dotnet test

Les versions exactes des packages évoluent. Garder les packages EF Core sur la même version majeure que le provider SQLite et le SDK ciblé ; utiliser les versions stables compatibles proposées par NuGet.
