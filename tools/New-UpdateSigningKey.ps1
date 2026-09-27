param(
    [string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Gyeona\TayoPinball\Signing\update-signing-key.dat'),
    [string]$PublicKeyPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($PublicKeyPath)) {
    $PublicKeyPath = Join-Path $root 'update-public-key.xml'
}

$utf8NoBom = New-Object Text.UTF8Encoding($false)
$entropy = [Text.Encoding]::UTF8.GetBytes('TayoPinball update signing key v1')
Add-Type -AssemblyName System.Security

function New-RsaProvider {
    $parameters = New-Object Security.Cryptography.CspParameters
    $parameters.ProviderType = 24
    $provider = New-Object Security.Cryptography.RSACryptoServiceProvider(3072, $parameters)
    $provider.PersistKeyInCsp = $false
    return $provider
}

function Read-ProtectedPrivateKey {
    param([string]$Path)

    $protectedBytes = [IO.File]::ReadAllBytes($Path)
    $plainBytes = [Security.Cryptography.ProtectedData]::Unprotect(
        $protectedBytes,
        $entropy,
        [Security.Cryptography.DataProtectionScope]::CurrentUser)
    try {
        return [Text.Encoding]::UTF8.GetString($plainBytes)
    }
    finally {
        [Array]::Clear($plainBytes, 0, $plainBytes.Length)
    }
}

$rsa = New-RsaProvider
try {
    if ((Test-Path -LiteralPath $PrivateKeyPath) -and !$Force) {
        $rsa.FromXmlString((Read-ProtectedPrivateKey -Path $PrivateKeyPath))
        Write-Host "Reusing protected signing key: $PrivateKeyPath"
    }
    else {
        $privateParent = Split-Path -Parent $PrivateKeyPath
        New-Item -ItemType Directory -Force -Path $privateParent | Out-Null

        $privateBytes = [Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($true))
        try {
            $protectedBytes = [Security.Cryptography.ProtectedData]::Protect(
                $privateBytes,
                $entropy,
                [Security.Cryptography.DataProtectionScope]::CurrentUser)
            [IO.File]::WriteAllBytes($PrivateKeyPath, $protectedBytes)
        }
        finally {
            [Array]::Clear($privateBytes, 0, $privateBytes.Length)
        }
        Write-Host "Created protected signing key: $PrivateKeyPath"
    }

    $publicParent = Split-Path -Parent $PublicKeyPath
    New-Item -ItemType Directory -Force -Path $publicParent | Out-Null
    [IO.File]::WriteAllText($PublicKeyPath, $rsa.ToXmlString($false) + [Environment]::NewLine, $utf8NoBom)
    Write-Host "Wrote public key: $PublicKeyPath"
    Write-Warning 'Back up the protected private key. Losing it prevents future in-app updates for released builds.'
}
finally {
    $rsa.Dispose()
}
