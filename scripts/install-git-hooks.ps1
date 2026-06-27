param(
    [string]$RepoRoot = (Get-Location).Path
)

$hookPath = Join-Path $RepoRoot ".githooks"
git config core.hooksPath $hookPath
Write-Host "Configured git hooks path to $hookPath"
