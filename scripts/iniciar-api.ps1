$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\backend\ControleFrotas.Api')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'Na primeira execução, este usuário será criado como administrador da empresa inicial.'
$env:Bootstrap__Username = Read-Host 'Usuário administrador'
$fleetSecret = Read-Host 'Senha inicial (mínimo 12 caracteres)' -AsSecureString
$env:Bootstrap__Password = [System.Net.NetworkCredential]::new('', $fleetSecret).Password
try {
    if ($env:Bootstrap__Password.Length -lt 12) {
        throw "Informe uma senha inicial com pelo menos 12 caracteres e execute novamente."
    }
    dotnet run --project ControleFrotas.Api.csproj --urls http://localhost:5038
    if ($LASTEXITCODE -ne 0) { throw "A API não iniciou. Consulte o erro apresentado acima." }
} finally {
    Remove-Item Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Username -ErrorAction SilentlyContinue
}
