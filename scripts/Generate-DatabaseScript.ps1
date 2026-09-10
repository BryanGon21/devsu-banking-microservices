[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory
$templatePath = Join-Path $repositoryRoot 'database\database-bootstrap.template.sql'
$outputPath = Join-Path $repositoryRoot 'BaseDatos.sql'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("devsu-database-" + [Guid]::NewGuid().ToString('N'))
$customersScriptPath = Join-Path $temporaryDirectory 'customers-migrations.sql'
$accountsScriptPath = Join-Path $temporaryDirectory 'accounts-migrations.sql'

New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    Push-Location $repositoryRoot
    try {
        $customersProject = 'src\Customers\Devsu.Customers.Infrastructure'
        $accountsProject = 'src\Accounts\Devsu.Accounts.Infrastructure'

        & dotnet restore $customersProject --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore the Customers database project in locked mode.'
        }

        & dotnet restore $accountsProject --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore the Accounts database project in locked mode.'
        }

        & dotnet build $customersProject --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not build the Customers database project.'
        }

        & dotnet build $accountsProject --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not build the Accounts database project.'
        }

        & dotnet ef migrations script `
            --idempotent `
            --no-build `
            --configuration Release `
            --project $customersProject `
            --startup-project $customersProject `
            --context CustomersDbContext `
            --output $customersScriptPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not generate the Customers database migration script.'
        }

        & dotnet ef migrations script `
            --idempotent `
            --no-build `
            --configuration Release `
            --project $accountsProject `
            --startup-project $accountsProject `
            --context AccountsDbContext `
            --output $accountsScriptPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not generate the Accounts database migration script.'
        }
    }
    finally {
        Pop-Location
    }

    $template = [IO.File]::ReadAllText($templatePath)
    if (-not $template.Contains('-- <CUSTOMERS_MIGRATIONS>') -or
        -not $template.Contains('-- <ACCOUNTS_MIGRATIONS>')) {
        throw 'The database template does not contain both migration markers.'
    }

    $customersMigrations = [IO.File]::ReadAllText($customersScriptPath)
    $accountsMigrations = [IO.File]::ReadAllText($accountsScriptPath)

    $generatedScript = $template.Replace(
        '-- <CUSTOMERS_MIGRATIONS>',
        $customersMigrations.Trim()).Replace(
        '-- <ACCOUNTS_MIGRATIONS>',
        $accountsMigrations.Trim())

    $normalizedScript = $generatedScript.Replace("`r`n", "`n").TrimEnd() + "`n"
    [IO.File]::WriteAllText(
        $outputPath,
        $normalizedScript,
        [Text.UTF8Encoding]::new($false))

    Write-Output "Generated $outputPath"
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
