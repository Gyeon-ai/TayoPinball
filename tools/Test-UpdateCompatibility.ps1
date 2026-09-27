param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [Parameter(Mandatory=$true)][string]$NamespaceName,
    [Parameter(Mandatory=$true)][string]$RepositoryRoot,
    [switch]$CheckPublishedFeed
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$flags = [Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ExePath).Path)
$updater = $assembly.GetType("$NamespaceName.SelfUpdater", $true)
$manifestType = $assembly.GetType("$NamespaceName.UpdateManifest", $true)
$productType = $assembly.GetType("$NamespaceName.UpdateProduct", $true)
$product = if ($assembly.GetName().Name.EndsWith('Auto')) { 'Auto' } else { 'Standard' }
function Invoke-Updater([string]$Name, [object[]]$Values) {
    $normalized = New-Object object[] $Values.Length
    for ($i = 0; $i -lt $Values.Length; $i++) {
        $normalized[$i] = $Values[$i].PSObject.BaseObject
    }
    return $updater.GetMethod($Name, $flags).Invoke($null, $normalized)
}
$manifestBytes = [IO.File]::ReadAllBytes((Join-Path $root 'update.json'))
$signatureBytes = [IO.File]::ReadAllBytes((Join-Path $root 'update.json.sig'))
Invoke-Updater 'VerifyManifestSignature' @($manifestBytes, $signatureBytes) | Out-Null
$serializer = New-Object Web.Script.Serialization.JavaScriptSerializer
$manifest = $serializer.Deserialize([Text.Encoding]::UTF8.GetString($manifestBytes), $manifestType)
if ($manifest.SchemaVersion -ne 2) { throw 'Unexpected manifest schema' }
if ($updater.GetMethod('ValidateManifestIdentity', $flags)) {
    Invoke-Updater 'ValidateManifestIdentity' @($manifest) | Out-Null
}
$version = [Version]$manifest.Version
$file = $manifest.$product
Invoke-Updater 'ValidateManifestFile' @($file) | Out-Null
$builtExe = Join-Path $root ("build\Release\{0}\{0}.exe" -f $assembly.GetName().Name)
Invoke-Updater 'ValidateAssemblyIdentity' @($builtExe, $assembly.GetName().Name, $version) | Out-Null
if ((Get-FileHash -LiteralPath $builtExe -Algorithm SHA256).Hash -ne $file.Sha256 -or
    (Get-Item -LiteralPath $builtExe).Length -ne $file.Size) { throw 'Manifest does not match final EXE' }

if ($CheckPublishedFeed) {
    # Invoke the real read-only startup check, without a form or installer.
    $configuration = Invoke-Updater 'GetConfiguration' @([Enum]::Parse($productType, $product))
    $task = Invoke-Updater 'GetUpdateCandidateAsync' @($configuration)
    if (!$task.Wait(30000)) { throw 'Published update check timed out' }
    $candidate = $task.GetAwaiter().GetResult()
    if ($version -gt $assembly.GetName().Version) {
        if ($null -eq $candidate -or $candidate.Version -ne $version -or
            $candidate.File.Sha256 -ne $file.Sha256 -or $candidate.File.Url -cne $file.Url) {
            throw 'Published update candidate does not match the release'
        }
    } elseif ($null -ne $candidate) { throw 'Latest client should not offer an update' }
}
Write-Output ("PASS: {0} {1} accepts {2}; published-feed={3}" -f $assembly.GetName().Name, $assembly.GetName().Version, $version, [bool]$CheckPublishedFeed)
