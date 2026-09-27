param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Gyeona\TayoPinball\Signing\update-signing-key.dat'),
    [string]$PublicKeyPath,
    [Security.SecureString]$Password,
    [Security.SecureString]$ConfirmPassword,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$root = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($PublicKeyPath)) {
    $PublicKeyPath = Join-Path $root 'update-public-key.xml'
}

$schemaVersion = 1
$algorithm = 'PBKDF2-HMAC-SHA256/AES-256-CBC/HMAC-SHA256'
$iterations = 600000
$entropy = [Text.Encoding]::UTF8.GetBytes('TayoPinball update signing key v1')
$utf8NoBom = New-Object Text.UTF8Encoding($false)

function New-RsaProvider {
    $parameters = New-Object Security.Cryptography.CspParameters
    $parameters.ProviderType = 24
    $provider = New-Object Security.Cryptography.RSACryptoServiceProvider(3072, $parameters)
    $provider.PersistKeyInCsp = $false
    return $provider
}

function ConvertFrom-SecurePassword {
    param([Security.SecureString]$SecurePassword)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
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

function Assert-PublicKeyMatch {
    param(
        [Security.Cryptography.RSACryptoServiceProvider]$PrivateRsa,
        [string]$ExpectedPublicKeyPath
    )

    $expectedRsa = New-RsaProvider
    try {
        $expectedRsa.FromXmlString([IO.File]::ReadAllText($ExpectedPublicKeyPath))
        $actual = $PrivateRsa.ExportParameters($false)
        $expected = $expectedRsa.ExportParameters($false)
        if (![Convert]::ToBase64String($actual.Modulus).Equals([Convert]::ToBase64String($expected.Modulus)) -or
            ![Convert]::ToBase64String($actual.Exponent).Equals([Convert]::ToBase64String($expected.Exponent))) {
            throw 'The private key does not match update-public-key.xml.'
        }
    }
    finally {
        $expectedRsa.Dispose()
    }
}

function Get-PublicKeyFingerprint {
    param([Security.Cryptography.RSACryptoServiceProvider]$Rsa)

    $publicBytes = [Text.Encoding]::UTF8.GetBytes($Rsa.ToXmlString($false))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($publicBytes))).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
        [Array]::Clear($publicBytes, 0, $publicBytes.Length)
    }
}

function Get-AuthenticatedData {
    param(
        [int]$IterationCount,
        [byte[]]$Salt,
        [byte[]]$Iv,
        [byte[]]$Ciphertext,
        [string]$Fingerprint
    )

    $magic = [Text.Encoding]::ASCII.GetBytes('TayoPinball.UpdateKey.v1')
    $iterationBytes = [BitConverter]::GetBytes($IterationCount)
    if ([BitConverter]::IsLittleEndian) {
        [Array]::Reverse($iterationBytes)
    }
    $fingerprintBytes = [Text.Encoding]::ASCII.GetBytes($Fingerprint)
    $result = New-Object byte[] ($magic.Length + $iterationBytes.Length + $Salt.Length + $Iv.Length + $Ciphertext.Length + $fingerprintBytes.Length)
    $offset = 0
    foreach ($part in @($magic, $iterationBytes, $Salt, $Iv, $Ciphertext, $fingerprintBytes)) {
        [Buffer]::BlockCopy($part, 0, $result, $offset, $part.Length)
        $offset += $part.Length
    }
    return $result
}

if (!(Test-Path -LiteralPath $PrivateKeyPath -PathType Leaf)) {
    throw "Protected signing key was not found: $PrivateKeyPath"
}
if (!(Test-Path -LiteralPath $PublicKeyPath -PathType Leaf)) {
    throw "Public key was not found: $PublicKeyPath"
}

$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ((Test-Path -LiteralPath $OutputPath) -and !$Force) {
    throw "Backup already exists. Use -Force to replace it: $OutputPath"
}

if ($null -eq $Password) {
    $Password = Read-Host 'Backup password (at least 14 characters)' -AsSecureString
}
if ($null -eq $ConfirmPassword) {
    $ConfirmPassword = Read-Host 'Confirm backup password' -AsSecureString
}

$passwordText = ConvertFrom-SecurePassword -SecurePassword $Password
$confirmText = ConvertFrom-SecurePassword -SecurePassword $ConfirmPassword
$privateXml = $null
$privateBytes = $null
$derivedBytes = $null
$encryptionKey = $null
$macKey = $null
$ciphertext = $null
$authenticatedData = $null
$salt = New-Object byte[] 32
$iv = New-Object byte[] 16
$rsa = New-RsaProvider
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
$kdf = $null
$aes = $null
$encryptor = $null
$hmac = $null
$temporaryPath = $null
$replacementBackupPath = $null

try {
    if ($passwordText.Length -lt 14) {
        throw 'The backup password must be at least 14 characters.'
    }
    if (![String]::Equals($passwordText, $confirmText, [StringComparison]::Ordinal)) {
        throw 'The backup passwords do not match.'
    }

    $privateXml = Read-ProtectedPrivateKey -Path $PrivateKeyPath
    $rsa.FromXmlString($privateXml)
    Assert-PublicKeyMatch -PrivateRsa $rsa -ExpectedPublicKeyPath $PublicKeyPath
    $fingerprint = Get-PublicKeyFingerprint -Rsa $rsa

    $random.GetBytes($salt)
    $random.GetBytes($iv)
    $kdf = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $passwordText,
        $salt,
        $iterations,
        [Security.Cryptography.HashAlgorithmName]::SHA256)
    $derivedBytes = $kdf.GetBytes(64)
    $encryptionKey = New-Object byte[] 32
    $macKey = New-Object byte[] 32
    [Buffer]::BlockCopy($derivedBytes, 0, $encryptionKey, 0, 32)
    [Buffer]::BlockCopy($derivedBytes, 32, $macKey, 0, 32)

    $privateBytes = [Text.Encoding]::UTF8.GetBytes($privateXml)
    $aes = [Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.BlockSize = 128
    $aes.Mode = [Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key = $encryptionKey
    $aes.IV = $iv
    $encryptor = $aes.CreateEncryptor()
    $ciphertext = $encryptor.TransformFinalBlock($privateBytes, 0, $privateBytes.Length)

    $authenticatedData = Get-AuthenticatedData -IterationCount $iterations -Salt $salt -Iv $iv -Ciphertext $ciphertext -Fingerprint $fingerprint
    $hmac = New-Object Security.Cryptography.HMACSHA256(, $macKey)
    $mac = $hmac.ComputeHash($authenticatedData)

    $package = [ordered]@{
        SchemaVersion = $schemaVersion
        Algorithm = $algorithm
        Iterations = $iterations
        Salt = [Convert]::ToBase64String($salt)
        Iv = [Convert]::ToBase64String($iv)
        Ciphertext = [Convert]::ToBase64String($ciphertext)
        Mac = [Convert]::ToBase64String($mac)
        PublicKeyFingerprint = $fingerprint
    }

    $outputParent = Split-Path -Parent $OutputPath
    [IO.Directory]::CreateDirectory($outputParent) | Out-Null
    $temporaryPath = Join-Path $outputParent ((Split-Path -Leaf $OutputPath) + '.tmp.' + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($temporaryPath, ($package | ConvertTo-Json), $utf8NoBom)
    if (Test-Path -LiteralPath $OutputPath) {
        $replacementBackupPath = Join-Path $outputParent ((Split-Path -Leaf $OutputPath) + '.old.' + [Guid]::NewGuid().ToString('N'))
        [IO.File]::Replace($temporaryPath, $OutputPath, $replacementBackupPath, $true)
        [IO.File]::Delete($replacementBackupPath)
        $replacementBackupPath = $null
    }
    else {
        [IO.File]::Move($temporaryPath, $OutputPath)
    }
    $temporaryPath = $null

    Write-Host "Created portable signing-key backup: $OutputPath"
    Write-Host "Public-key fingerprint: $fingerprint"
    Write-Warning 'Keep the .tayokey file and its password in separate secure locations. Never upload the backup to GitHub.'
}
finally {
    if ($temporaryPath -and (Test-Path -LiteralPath $temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    if ($replacementBackupPath -and (Test-Path -LiteralPath $replacementBackupPath)) {
        Remove-Item -LiteralPath $replacementBackupPath -Force
    }
    foreach ($disposable in @($hmac, $encryptor, $aes, $kdf, $random, $rsa)) {
        if ($null -ne $disposable) {
            $disposable.Dispose()
        }
    }
    foreach ($bytes in @($privateBytes, $derivedBytes, $encryptionKey, $macKey, $authenticatedData)) {
        if ($null -ne $bytes) {
            [Array]::Clear($bytes, 0, $bytes.Length)
        }
    }
    $passwordText = $null
    $confirmText = $null
    $privateXml = $null
}
