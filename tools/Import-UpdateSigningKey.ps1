param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Gyeona\TayoPinball\Signing\update-signing-key.dat'),
    [string]$PublicKeyPath,
    [Security.SecureString]$Password,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$root = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($PublicKeyPath)) {
    $PublicKeyPath = Join-Path $root 'update-public-key.xml'
}

$expectedAlgorithm = 'PBKDF2-HMAC-SHA256/AES-256-CBC/HMAC-SHA256'
$entropy = [Text.Encoding]::UTF8.GetBytes('TayoPinball update signing key v1')

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
            throw 'The backup key does not match update-public-key.xml.'
        }
    }
    finally {
        $expectedRsa.Dispose()
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

function Test-FixedTimeEqual {
    param([byte[]]$Left, [byte[]]$Right)

    if ($Left.Length -ne $Right.Length) {
        return $false
    }
    $difference = 0
    for ($index = 0; $index -lt $Left.Length; $index++) {
        $difference = $difference -bor ($Left[$index] -bxor $Right[$index])
    }
    return $difference -eq 0
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

$InputPath = [IO.Path]::GetFullPath($InputPath)
$PrivateKeyPath = [IO.Path]::GetFullPath($PrivateKeyPath)
if (!(Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "Portable signing-key backup was not found: $InputPath"
}
if (!(Test-Path -LiteralPath $PublicKeyPath -PathType Leaf)) {
    throw "Public key was not found: $PublicKeyPath"
}
if ((Get-Item -LiteralPath $InputPath).Length -gt 131072) {
    throw 'The portable signing-key backup is unexpectedly large.'
}
if ($null -eq $Password) {
    $Password = Read-Host 'Backup password' -AsSecureString
}

$passwordText = ConvertFrom-SecurePassword -SecurePassword $Password
$derivedBytes = $null
$encryptionKey = $null
$macKey = $null
$authenticatedData = $null
$plainBytes = $null
$protectedBytes = $null
$privateXml = $null
$kdf = $null
$hmac = $null
$aes = $null
$decryptor = $null
$rsa = New-RsaProvider
$temporaryPath = $null

try {
    try {
        $package = [IO.File]::ReadAllText($InputPath) | ConvertFrom-Json
        if ([int]$package.SchemaVersion -ne 1 -or [string]$package.Algorithm -ne $expectedAlgorithm) {
            throw 'Unsupported portable signing-key backup format.'
        }

        $iterations = [int]$package.Iterations
        if ($iterations -lt 200000 -or $iterations -gt 2000000) {
            throw 'The backup uses an invalid key-derivation iteration count.'
        }
        $fingerprint = [string]$package.PublicKeyFingerprint
        if ($fingerprint -notmatch '^[0-9A-F]{64}$') {
            throw 'The backup contains an invalid public-key fingerprint.'
        }

        $salt = [Convert]::FromBase64String([string]$package.Salt)
        $iv = [Convert]::FromBase64String([string]$package.Iv)
        $ciphertext = [Convert]::FromBase64String([string]$package.Ciphertext)
        $storedMac = [Convert]::FromBase64String([string]$package.Mac)
        if ($salt.Length -ne 32 -or $iv.Length -ne 16 -or $storedMac.Length -ne 32 -or
            $ciphertext.Length -eq 0 -or $ciphertext.Length -gt 65536 -or ($ciphertext.Length % 16) -ne 0) {
            throw 'The portable signing-key backup has invalid field lengths.'
        }
    }
    catch {
        throw "Cannot read the portable signing-key backup: $($_.Exception.Message)"
    }

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

    $authenticatedData = Get-AuthenticatedData -IterationCount $iterations -Salt $salt -Iv $iv -Ciphertext $ciphertext -Fingerprint $fingerprint
    $hmac = New-Object Security.Cryptography.HMACSHA256(, $macKey)
    $computedMac = $hmac.ComputeHash($authenticatedData)
    if (!(Test-FixedTimeEqual -Left $storedMac -Right $computedMac)) {
        throw 'The backup password is incorrect or the backup file was modified.'
    }

    $aes = [Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.BlockSize = 128
    $aes.Mode = [Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key = $encryptionKey
    $aes.IV = $iv
    $decryptor = $aes.CreateDecryptor()
    $plainBytes = $decryptor.TransformFinalBlock($ciphertext, 0, $ciphertext.Length)
    if ($plainBytes.Length -gt 65536) {
        throw 'The decrypted signing key is unexpectedly large.'
    }
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    $privateXml = $strictUtf8.GetString($plainBytes)

    $rsa.FromXmlString($privateXml)
    Assert-PublicKeyMatch -PrivateRsa $rsa -ExpectedPublicKeyPath $PublicKeyPath
    $actualFingerprint = Get-PublicKeyFingerprint -Rsa $rsa
    if (![String]::Equals($actualFingerprint, $fingerprint, [StringComparison]::Ordinal)) {
        throw 'The backup public-key fingerprint does not match its private key.'
    }

    if (Test-Path -LiteralPath $PrivateKeyPath -PathType Leaf) {
        $existingRsa = New-RsaProvider
        try {
            $existingRsa.FromXmlString((Read-ProtectedPrivateKey -Path $PrivateKeyPath))
            if ([String]::Equals((Get-PublicKeyFingerprint -Rsa $existingRsa), $actualFingerprint, [StringComparison]::Ordinal)) {
                Write-Host "The installed signing key already matches this backup: $PrivateKeyPath"
                return
            }
        }
        finally {
            $existingRsa.Dispose()
        }
        if (!$Force) {
            throw "A different signing key already exists. Use -Force to replace it: $PrivateKeyPath"
        }
    }

    $protectedBytes = [Security.Cryptography.ProtectedData]::Protect(
        $plainBytes,
        $entropy,
        [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $privateParent = Split-Path -Parent $PrivateKeyPath
    [IO.Directory]::CreateDirectory($privateParent) | Out-Null
    $temporaryPath = Join-Path $privateParent ((Split-Path -Leaf $PrivateKeyPath) + '.tmp.' + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllBytes($temporaryPath, $protectedBytes)

    $verificationRsa = New-RsaProvider
    try {
        $verificationRsa.FromXmlString((Read-ProtectedPrivateKey -Path $temporaryPath))
        if (![String]::Equals((Get-PublicKeyFingerprint -Rsa $verificationRsa), $actualFingerprint, [StringComparison]::Ordinal)) {
            throw 'The new DPAPI-protected key failed verification before installation.'
        }
    }
    finally {
        $verificationRsa.Dispose()
    }

    if (Test-Path -LiteralPath $PrivateKeyPath) {
        $backupPath = $PrivateKeyPath + '.bak-' + (Get-Date -Format 'yyyyMMddHHmmssfff')
        [IO.File]::Replace($temporaryPath, $PrivateKeyPath, $backupPath, $true)
        Write-Host "Preserved previous protected key: $backupPath"
    }
    else {
        [IO.File]::Move($temporaryPath, $PrivateKeyPath)
    }
    $temporaryPath = $null

    Write-Host "Imported signing key for the current Windows account: $PrivateKeyPath"
    Write-Host "Public-key fingerprint: $actualFingerprint"
}
finally {
    if ($temporaryPath -and (Test-Path -LiteralPath $temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    foreach ($disposable in @($decryptor, $aes, $hmac, $kdf, $rsa)) {
        if ($null -ne $disposable) {
            $disposable.Dispose()
        }
    }
    foreach ($bytes in @($plainBytes, $protectedBytes, $derivedBytes, $encryptionKey, $macKey, $authenticatedData)) {
        if ($null -ne $bytes) {
            [Array]::Clear($bytes, 0, $bytes.Length)
        }
    }
    $passwordText = $null
    $privateXml = $null
}
