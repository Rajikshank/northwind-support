# Setup before implementation

Initial prerequisite snapshot, 19 October 2026. Implementation update: .NET 10.0.402 was subsequently located under Program Files and used successfully; Docker engine became reachable after user setup, but image extraction filled C:. Docker execution is now deferred by request. Tests/builds run; see EVIDENCE.md for current results. The table below preserves the initial setup findings and must not be mistaken for final verification status.

## Current machine

| Component | Observed status | Required action |
| --- | --- | --- |
| Windows | Windows 11 Home x64, virtualization enabled, about 15.4 GiB usable RAM | Use Linux containers through WSL 2. |
| Git | 2.53.0 available | Nothing to install; fork the repository under your own GitHub account. |
| Node/npm | Node 24.13.1 and npm available; .nvmrc specifies 22 | Prefer Node 22 for exact repository parity. Node 24 can first be checked against the locked dependencies; do not silently rewrite the lockfile or upgrade packages. |
| WSL | WSL 2.6.3.0, Ubuntu, default WSL 2 | Already installed. No need to reinstall Ubuntu/WSL. |
| Docker Desktop | 4.84.0 installed per-user; client 29.6.2 and Compose v5.3.1 run by absolute path | Start Desktop, select WSL 2/Linux containers, resolve CLI PATH and verify engine connection. Reinstallation is not the first step. |
| .NET | Not found on PATH or usual machine/user locations | Locate any custom installation; otherwise install the x64 .NET 10 SDK, not just ASP.NET runtime. |
| Editor | VS Code installed | Existing editor is enough. C# tooling is optional convenience. |
| GitHub CLI | Not on PATH | Optional only; browser GitHub fork creation plus git is sufficient. |
| App dependencies | node_modules absent; package-lock.json present | Run npm ci once runtime setup is ready. |

Docker registry location: `%LOCALAPPDATA%\Programs\DockerDesktop`. The client currently reports no reachable Docker engine pipe. Docker installation alone does not prove containers can run.

## Install or activate

1. **.NET 10 SDK x64**: use the [official download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or `winget install --id Microsoft.DotNet.SDK.10 --exact`. Open a fresh terminal afterward. global.json requests 10.0.100 with latestFeature roll-forward; confirm `dotnet --version` inside this repository resolves a compatible 10.0 SDK. Repository package restore must succeed; an SDK version alone is not a passing build.
2. **Docker Desktop already exists**: launch it from the Start menu and finish any required first-run setup. Enable the WSL 2 backend and Linux containers. Add the verified resources/bin directory to your user PATH if needed, or use the absolute executable path below. No Windows containers are needed.
3. **Node 22, preferred**: use the [official Node releases](https://nodejs.org/en/download) or an existing version manager. Do not replace the current global Node installation without considering other projects. Node 24 is already installed, so a Node download is not automatically necessary if its baseline passes and the version choice is documented.

Do not install SQL Server separately: Compose runs the required SQL Server 2022 container. No paid cloud account, CAPTCHA, AI API key, Redis, Kubernetes, full Visual Studio, external identity provider or global EF CLI is required. The repository local .NET tools are restored with dotnet tool restore.

## Verify from PowerShell

Run these individually, investigating errors before proceeding:

```powershell
git --version
node --version
npm --version
dotnet --info
dotnet --version
wsl --version
docker version
docker compose version
```

`docker version` must show both Client and Server. If docker is not on PATH, use:

```powershell
& "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe" version
& "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe" compose version
```

Once the engine is reachable, check its operating system is Linux:

```powershell
docker info --format '{{.OSType}}'
```

Check available disk space before downloading the SDK and SQL Server image. Prefer keeping several GB free for images, package caches and database volumes; actual usage varies. If the OneDrive workspace causes file-lock/watch problems, diagnose first and use a normal local checkout if needed; do not move/delete the current checkout automatically.

## Repository baseline after prerequisites

Do not overwrite an existing .env. If absent, copy the local fixture example. Copying .env does not automatically export its values for dotnet run; existing Development defaults suffice for the original baseline, and new JWT environment setup must be documented during Task 3.

```powershell
npm ci
dotnet tool restore
npm run db:up
docker compose ps
npm test
npm run build
npm run lint
npm run typecheck
```

Wait for SQL Server to become healthy before running the API. Launch in separate terminals:

```powershell
npm run api
```

```powershell
npm run web
```

For the original HTTPS API profile, trust the local development certificate if necessary:

```powershell
dotnet dev-certs https --trust
```

This is local prerequisite setup, not a certificate-management feature. Alternatively use the existing HTTP profile/proxy configuration. Verify launchSettings and the actual listening URL before choosing proxy target.

After Task 4, the documented Windows proxy command will be:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'
npm run web
```

Final container authentication and health checks are defined in ASSESSMENT_PLAN.md. Do not reset/delete an existing database volume merely to check setup; use a separate disposable Compose project for destructive clean-volume verification.

Sources: [Microsoft .NET Windows installation](https://learn.microsoft.com/en-us/dotnet/core/install/windows), [Docker Windows setup](https://docs.docker.com/desktop/setup/install/windows-install/), [Docker WSL backend](https://docs.docker.com/desktop/features/wsl/), and repository README/global.json/.nvmrc/package-lock.json. Machine status above comes from read-only checks in this session.
