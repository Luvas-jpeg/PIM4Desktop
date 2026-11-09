[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/screenshots')
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') {
    throw 'A captura usa o WPF real e precisa ser executada no Windows com o SDK .NET 10.'
}

$repository = Split-Path $PSScriptRoot -Parent
$capturePath = [System.IO.Path]::GetFullPath($OutputDirectory)
$previousCommit = $env:PIM4_CAPTURE_COMMIT
Push-Location $repository
try {
    $revision = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar a revisão do repositório.' }
    $changes = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível conferir as alterações locais.' }
    $env:PIM4_CAPTURE_COMMIT = if ($changes) { "$revision (com alterações locais)" } else { "$revision" }
    & dotnet run --project tests/DesktopUiChecks/DesktopUiChecks.csproj --configuration Release -- --screenshots $capturePath
    if ($LASTEXITCODE -ne 0) { throw 'A verificação WPF ou a geração das capturas falhou. Confira o erro acima.' }
    Write-Host "Capturas e manifesto salvos em: $capturePath"
} finally {
    $env:PIM4_CAPTURE_COMMIT = $previousCommit
    Pop-Location
}
