$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$errors = New-Object System.Collections.Generic.List[string]

if (-not (Test-Path "infra/mautrix/config.yaml.template")) {
    $errors.Add("Missing infra/mautrix/config.yaml.template.")
}

$gitignore = if (Test-Path ".gitignore") {
    Get-Content ".gitignore" -Raw
} else {
    ""
}

if ($gitignore -notmatch "(?m)^infra/mautrix/config\.yaml\s*$") {
    $errors.Add("Missing infra/mautrix/config.yaml ignore rule in .gitignore.")
}

cmd /c "git ls-files --error-unmatch -- infra/mautrix/config.yaml >nul 2>nul"
if ($LASTEXITCODE -eq 0) {
    $errors.Add("infra/mautrix/config.yaml is still tracked by git.")
}

$readme = if (Test-Path "infra/README.md") {
    Get-Content "infra/README.md" -Raw
} else {
    ""
}

if ($readme -notmatch "infra/mautrix/config\.yaml\.template") {
    $errors.Add("infra/README.md must explain local mautrix config bootstrap from template.")
}

if ($errors.Count -gt 0) {
    throw ($errors -join [Environment]::NewLine)
}

Write-Host "Local mautrix config guard passed."
