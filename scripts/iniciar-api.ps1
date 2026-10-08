$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\backend')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'Na primeira execução, este usuário será criado como administrador da empresa inicial.'
$env:Bootstrap__Username = Read-Host 'Usuário administrador'
$fleetSecret = Read-Host 'Senha inicial (mínimo 12 caracteres)' -AsSecureString
$env:Bootstrap__Password = [System.Net.NetworkCredential]::new('', $fleetSecret).Password
try {
    dotnet run --project ControleFrotas.Api.csproj --urls http://localhost:5038
} finally {
    Remove-Item Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Username -ErrorAction SilentlyContinue
}
