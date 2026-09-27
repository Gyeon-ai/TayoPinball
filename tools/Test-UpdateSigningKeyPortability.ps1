$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$root = Split-Path -Parent $PSScriptRoot
$exportScript = Join-Path $PSScriptRoot 'Export-UpdateSigningKey.ps1'
$importScript = Join-Path $PSScriptRoot 'Import-UpdateSigningKey.ps1'
$publicKeyPath = Join-Path $root 'update-public-key.xml'
$sourcePrivateKeyPath = Join-Path $env:LOCALAPPDATA 'Gyeona\TayoPinball\Signing\update-signing-key.dat'
$entropy = [Text.Encoding]::UTF8.GetBytes('TayoPinball update signing key v1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('TayoPinball-KeyPortability-' + [Guid]::NewGuid().ToString('N'))

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

function Write-RandomProtectedPrivateKey {
    param([string]$Path)

    $randomRsa = New-RsaProvider
    $plainBytes = $null
    try {
        $plainBytes = [Text.Encoding]::UTF8.GetBytes($randomRsa.ToXmlString($true))
        $protectedBytes = [Security.Cryptography.ProtectedData]::Protect(
            $plainBytes,
            $entropy,
            [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.File]::WriteAllBytes($Path, $protectedBytes)
    }
    finally {
        $randomRsa.Dispose()
        if ($null -ne $plainBytes) {
            [Array]::Clear($plainBytes, 0, $plainBytes.Length)
        }
    }
}

if (!(Test-Path -LiteralPath $sourcePrivateKeyPath -PathType Leaf)) {
    throw "Source signing key was not found: $sourcePrivateKeyPath"
}

[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
$passwordBytes = New-Object byte[] 48
$random.GetBytes($passwordBytes)
$passwordText = [Convert]::ToBase64String($passwordBytes)
$password = ConvertTo-SecureString $passwordText -AsPlainText -Force
$wrongPassword = ConvertTo-SecureString ($passwordText + '-wrong') -AsPlainText -Force
[Array]::Clear($passwordBytes, 0, $passwordBytes.Length)
$passwordText = $null

try {
    $backupPath = Join-Path $testRoot 'portable-key.tayokey'
    $importedPath = Join-Path $testRoot 'imported-key.dat'
    & $exportScript -OutputPath $backupPath -PrivateKeyPath $sourcePrivateKeyPath -PublicKeyPath $publicKeyPath -Password $password -ConfirmPassword $password
    $firstBackupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
    & $exportScript -OutputPath $backupPath -PrivateKeyPath $sourcePrivateKeyPath -PublicKeyPath $publicKeyPath -Password $password -ConfirmPassword $password -Force
    if ((Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -eq $firstBackupHash) {
        throw 'Forced backup replacement did not produce fresh encryption parameters.'
    }
    & $importScript -InputPath $backupPath -PrivateKeyPath $importedPath -PublicKeyPath $publicKeyPath -Password $password

    if (!(Test-Path -LiteralPath $backupPath -PathType Leaf) -or !(Test-Path -LiteralPath $importedPath -PathType Leaf)) {
        throw 'The portability test did not create the expected files.'
    }

    $privateRsa = New-RsaProvider
    $publicRsa = New-RsaProvider
    $message = [Text.Encoding]::UTF8.GetBytes('TayoPinball portable signing-key verification')
    try {
        $privateRsa.FromXmlString((Read-ProtectedPrivateKey -Path $importedPath))
        $publicRsa.FromXmlString([IO.File]::ReadAllText($publicKeyPath))
        $signature = $privateRsa.SignData($message, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
        if (!$publicRsa.VerifyData($message, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'), $signature)) {
            throw 'The imported signing key could not produce a valid signature.'
        }
    }
    finally {
        $privateRsa.Dispose()
        $publicRsa.Dispose()
        [Array]::Clear($message, 0, $message.Length)
    }

    $wrongPasswordPath = Join-Path $testRoot 'wrong-password-key.dat'
    $wrongPasswordRejected = $false
    try {
        & $importScript -InputPath $backupPath -PrivateKeyPath $wrongPasswordPath -PublicKeyPath $publicKeyPath -Password $wrongPassword
    }
    catch {
        $wrongPasswordRejected = $true
    }
    if (!$wrongPasswordRejected -or (Test-Path -LiteralPath $wrongPasswordPath)) {
        throw 'A wrong password was not safely rejected.'
    }

    $tamperedPath = Join-Path $testRoot 'tampered-key.tayokey'
    $package = [IO.File]::ReadAllText($backupPath) | ConvertFrom-Json
    $tamperedCiphertext = [Convert]::FromBase64String([string]$package.Ciphertext)
    $tamperedCiphertext[0] = $tamperedCiphertext[0] -bxor 1
    $package.Ciphertext = [Convert]::ToBase64String($tamperedCiphertext)
    [Array]::Clear($tamperedCiphertext, 0, $tamperedCiphertext.Length)
    $utf8NoBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($tamperedPath, ($package | ConvertTo-Json), $utf8NoBom)

    $tamperedImportPath = Join-Path $testRoot 'tampered-key.dat'
    $tamperRejected = $false
    try {
        & $importScript -InputPath $tamperedPath -PrivateKeyPath $tamperedImportPath -PublicKeyPath $publicKeyPath -Password $password
    }
    catch {
        $tamperRejected = $true
    }
    if (!$tamperRejected -or (Test-Path -LiteralPath $tamperedImportPath)) {
        throw 'A modified backup was not safely rejected.'
    }

    $differentKeyPath = Join-Path $testRoot 'different-key.dat'
    Write-RandomProtectedPrivateKey -Path $differentKeyPath
    $differentKeyHash = (Get-FileHash -LiteralPath $differentKeyPath -Algorithm SHA256).Hash
    $replacementRejected = $false
    try {
        & $importScript -InputPath $backupPath -PrivateKeyPath $differentKeyPath -PublicKeyPath $publicKeyPath -Password $password
    }
    catch {
        $replacementRejected = $true
    }
    if (!$replacementRejected -or (Get-FileHash -LiteralPath $differentKeyPath -Algorithm SHA256).Hash -ne $differentKeyHash) {
        throw 'A different installed key was not preserved when -Force was omitted.'
    }

    & $importScript -InputPath $backupPath -PrivateKeyPath $differentKeyPath -PublicKeyPath $publicKeyPath -Password $password -Force
    $preservedKeys = @(Get-ChildItem -LiteralPath $testRoot -Filter 'different-key.dat.bak-*')
    if ($preservedKeys.Count -ne 1 -or (Get-FileHash -LiteralPath $preservedKeys[0].FullName -Algorithm SHA256).Hash -ne $differentKeyHash) {
        throw 'Forced replacement did not preserve the previous protected key.'
    }
    & $importScript -InputPath $backupPath -PrivateKeyPath $differentKeyPath -PublicKeyPath $publicKeyPath -Password $password

    Write-Host 'Portable signing-key test passed: export, import, signing, rejection checks, atomic replacement, and idempotent import.'
}
finally {
    $random.Dispose()
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolvedTestRoot.StartsWith($resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the temporary directory: $resolvedTestRoot"
    }
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
