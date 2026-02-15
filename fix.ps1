# ================================
# Messaging Architecture Simplifier
# ================================

Write-Host "Starting refactor..." -ForegroundColor Cyan

# Detect solution file
$solution = Get-ChildItem *.sln | Select-Object -First 1
if (-not $solution) {
    Write-Host "No solution file found." -ForegroundColor Red
    exit 1
}

Write-Host "Using solution: $($solution.Name)" -ForegroundColor Yellow

# -------------------------
# 1️⃣ Remove Email Projects
# -------------------------

if (Test-Path ".\src\Email") {
    Write-Host "Removing /src/Email..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force ".\src\Email"
}

if (Test-Path ".\test") {
    Get-ChildItem ".\test" -Directory |
        Where-Object { $_.Name -like "Email*" } |
        ForEach-Object {
            Write-Host "Removing test project: $($_.FullName)" -ForegroundColor Yellow
            Remove-Item -Recurse -Force $_.FullName
        }
}

# Remove projects from solution
dotnet sln $solution.Name list | Where-Object { $_ -like "*Email*" } | ForEach-Object {
    dotnet sln $solution.Name remove $_
}

# -------------------------
# 2️⃣ Rename Platform Projects
# -------------------------

function Rename-ProjectFolder {
    param (
        [string]$OldPath,
        [string]$NewName
    )

    if (Test-Path $OldPath) {
        $parent = Split-Path $OldPath -Parent
        $newPath = Join-Path $parent $NewName

        Write-Host "Renaming $OldPath → $newPath" -ForegroundColor Yellow
        Rename-Item $OldPath $NewName
    }
}

Rename-ProjectFolder ".\src\Platform\Api" "Api"
Rename-ProjectFolder ".\src\Platform\Core" "Core"
Rename-ProjectFolder ".\src\Platform\Persistence" "Persistence"

# Remove empty Platform folder if applicable
if (Test-Path ".\src\Platform") {
    $remaining = Get-ChildItem ".\src\Platform"
    if ($remaining.Count -eq 0) {
        Remove-Item ".\src\Platform"
    }
}

# -------------------------
# 3️⃣ Rename Worker
# -------------------------

Rename-ProjectFolder ".\src\Workers\EmailDelivery" "Workers"

if (Test-Path ".\src\Workers") {
    $remaining = Get-ChildItem ".\src\Workers"
    if ($remaining.Count -eq 0) {
        Remove-Item ".\src\Workers"
    }
}

# -------------------------
# 4️⃣ Update Namespaces + Assembly Names
# -------------------------

Write-Host "Updating namespaces..." -ForegroundColor Cyan

Get-ChildItem -Recurse -Include *.cs,*.csproj | ForEach-Object {
    (Get-Content $_.FullName) `
        -replace "Messaging\.Platform\.Api", "Messaging.Api" `
        -replace "Messaging\.Platform\.Core", "Messaging.Core" `
        -replace "Messaging\.Platform\.Persistence", "Messaging.Persistence" `
        -replace "Messaging\.Workers\.EmailDelivery", "Messaging.Workers" `
        -replace "namespace Messaging\.Platform", "namespace Messaging" `
        -replace "using Messaging\.Platform", "using Messaging" `
        | Set-Content $_.FullName
}

# -------------------------
# 5️⃣ Re-add Projects to Solution
# -------------------------

Write-Host "Re-adding projects to solution..." -ForegroundColor Cyan

Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {
    dotnet sln $solution.Name add $_.FullName
}

# -------------------------
# 6️⃣ Restore + Build
# -------------------------

Write-Host "Restoring..." -ForegroundColor Cyan
dotnet restore

Write-Host "Building..." -ForegroundColor Cyan
dotnet build

Write-Host "Refactor complete." -ForegroundColor Green
