# Upload local MemoryScanner project to GitHub (RuojunYang/MemoryScanner)
$ErrorActionPreference = "Stop"

$RemoteUrl = "https://github.com/RuojunYang/MemoryScanner.git"
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $Root ".git"))) {
    Set-Location $Root
    git init
} else {
    Set-Location $Root
}

$remotes = git remote 2>$null
if ($remotes -contains "origin") {
    git remote set-url origin $RemoteUrl
} else {
    git remote add origin $RemoteUrl
}

git add -A
git status
$confirm = Read-Host "Commit and push to origin/main? (y/N)"
if ($confirm -notmatch '^[yY]') {
    Write-Host "Aborted."
    exit 0
}

$hasCommit = git diff --cached --quiet; $LASTEXITCODE -ne 0
if (-not $hasCommit) {
    git commit -m "Initial commit: AoE4 inferno perk memory scanner" `
        -m "Standalone memory scanner to modify inferno mode perks without Cheat Engine fingerprints."
}

git branch -M main
git pull origin main --allow-unrelated-histories --no-edit 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Note: pull failed or not needed; continuing with push..."
}
git push -u origin main
Write-Host "Done. See https://github.com/RuojunYang/MemoryScanner"
