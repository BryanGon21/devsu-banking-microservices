[CmdletBinding()]
param(
    [string]$CustomersApiBaseUrl = 'http://localhost:8081',
    [string]$AccountsApiBaseUrl = 'http://localhost:8082',
    [ValidateRange(1, 120)]
    [int]$EventPropagationTimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Net.Http

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory)]
        [System.Net.Http.HttpMethod]$Method,
        [Parameter(Mandatory)]
        [string]$RequestUri,
        [object]$Body,
        [hashtable]$Headers = @{}
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, $RequestUri)
    try {
        foreach ($header in $Headers.GetEnumerator()) {
            $request.Headers.Add($header.Key, [string]$header.Value)
        }

        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10 -Compress
            $request.Content = [System.Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                'application/json')
        }

        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $parsedBody = $null
            if (-not [string]::IsNullOrWhiteSpace($content) -and
                $response.Content.Headers.ContentType.MediaType -match 'json') {
                $parsedBody = $content | ConvertFrom-Json
            }

            return [PSCustomObject]@{
                StatusCode = [int]$response.StatusCode
                Content = $content
                Body = $parsedBody
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-StatusCode {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [int]$Expected,
        [Parameter(Mandatory)]
        [string]$Operation
    )

    if ($Response.StatusCode -ne $Expected) {
        throw "$Operation returned $($Response.StatusCode), expected ${Expected}: $($Response.Content)"
    }
}

$customersClient = [System.Net.Http.HttpClient]::new()
$accountsClient = [System.Net.Http.HttpClient]::new()
$customersClient.BaseAddress = [Uri]$CustomersApiBaseUrl
$accountsClient.BaseAddress = [Uri]$AccountsApiBaseUrl

try {
    $customersHealth = Invoke-ApiRequest `
        -Client $customersClient `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -RequestUri '/health/ready'
    $accountsHealth = Invoke-ApiRequest `
        -Client $accountsClient `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -RequestUri '/health/ready'
    Assert-StatusCode $customersHealth 200 'Customers readiness check'
    Assert-StatusCode $accountsHealth 200 'Accounts readiness check'

    $uniqueSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
    $identification = '9' + $uniqueSuffix.Substring($uniqueSuffix.Length - 9)
    $accountNumber = '8' + $uniqueSuffix.Substring($uniqueSuffix.Length - 7)
    $customerResponse = Invoke-ApiRequest `
        -Client $customersClient `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -RequestUri '/api/clientes' `
        -Body @{
            nombre = 'Container Smoke Customer'
            genero = 'Masculino'
            edad = 30
            identificacion = $identification
            direccion = 'Container Smoke Address'
            telefono = '0999999999'
            contrasena = [Guid]::NewGuid().ToString('N')
            estado = $true
        }
    Assert-StatusCode $customerResponse 201 'Customer creation'
    $customerId = [Guid]$customerResponse.Body.clienteId

    $accountDeadline = [DateTimeOffset]::UtcNow.AddSeconds($EventPropagationTimeoutSeconds)
    $accountResponse = $null
    while ([DateTimeOffset]::UtcNow -lt $accountDeadline) {
        $accountResponse = Invoke-ApiRequest `
            -Client $accountsClient `
            -Method ([System.Net.Http.HttpMethod]::Post) `
            -RequestUri '/api/cuentas' `
            -Body @{
                numeroCuenta = $accountNumber
                tipoCuenta = 'Ahorros'
                saldoInicial = 100.00
                estado = $true
                clienteId = $customerId
            }

        if ($accountResponse.StatusCode -eq 201) {
            break
        }

        if ($accountResponse.StatusCode -ne 409 -or
            $accountResponse.Body.code -ne 'cliente_no_disponible') {
            throw "Account creation failed unexpectedly: $($accountResponse.Content)"
        }

        Start-Sleep -Milliseconds 200
    }
    Assert-StatusCode $accountResponse 201 'Account creation after event propagation'

    $movementResponse = Invoke-ApiRequest `
        -Client $accountsClient `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -RequestUri '/api/movimientos' `
        -Headers @{ 'Idempotency-Key' = [Guid]::NewGuid().ToString('N') } `
        -Body @{
            numeroCuenta = $accountNumber
            tipoMovimiento = 'Deposito'
            valor = 25.00
        }
    Assert-StatusCode $movementResponse 201 'Deposit creation'
    if ([decimal]$movementResponse.Body.saldoDisponible -ne 125.00) {
        throw "Unexpected balance after deposit: $($movementResponse.Content)"
    }

    $insufficientFundsResponse = Invoke-ApiRequest `
        -Client $accountsClient `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -RequestUri '/api/movimientos' `
        -Headers @{ 'Idempotency-Key' = [Guid]::NewGuid().ToString('N') } `
        -Body @{
            numeroCuenta = $accountNumber
            tipoMovimiento = 'Retiro'
            valor = -200.00
        }
    Assert-StatusCode $insufficientFundsResponse 422 'Insufficient-funds withdrawal'
    if ($insufficientFundsResponse.Body.detail -cne 'Saldo no disponible') {
        throw "Unexpected insufficient-funds response: $($insufficientFundsResponse.Content)"
    }

    $date = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
    $statementResponse = Invoke-ApiRequest `
        -Client $accountsClient `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -RequestUri "/api/reportes?fechaInicio=$date&fechaFin=$date&clienteId=$customerId"
    Assert-StatusCode $statementResponse 200 'Account statement query'
    if ($statementResponse.Body.cuentas.Count -ne 1 -or
        [decimal]$statementResponse.Body.cuentas[0].saldoActual -ne 125.00 -or
        $statementResponse.Body.cuentas[0].movimientos.Count -ne 1) {
        throw "Unexpected account statement: $($statementResponse.Content)"
    }

    Write-Output (
        "STACK_SMOKE=OK customerId={0} accountNumber={1} balance=125.00" -f `
            $customerId,
            $accountNumber)
}
finally {
    $customersClient.Dispose()
    $accountsClient.Dispose()
}
