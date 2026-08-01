[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-p]{32}$')]
    [string]$ExtensionId
)

$ErrorActionPreference = 'Stop'

$hostName = 'com.pewpew.assistant.bridge'
$packageRoot = Join-Path $env:LOCALAPPDATA 'PewPew\NativeMessaging\current'
$hostPath = Join-Path $packageRoot 'PewPew.exe'
$manifestPath = Join-Path $packageRoot "$hostName.json"
$registryKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"

if (!(Test-Path -LiteralPath $hostPath) -or !(Test-Path -LiteralPath $manifestPath)) {
    throw 'native_package_or_manifest_missing'
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$expectedOrigin = "chrome-extension://$ExtensionId/"
if ($manifest.name -ne $hostName -or $manifest.type -ne 'stdio' -or
    $manifest.path -ne $hostPath -or $manifest.allowed_origins.Count -ne 1 -or
    $manifest.allowed_origins[0] -ne $expectedOrigin) {
    throw 'native_manifest_validation_failed'
}

New-Item -Path $registryKey -Force | Out-Null
Set-Item -Path $registryKey -Value $manifestPath
$readback = (Get-Item -LiteralPath $registryKey).GetValue('')
if ($readback -ne $manifestPath) {
    Remove-Item -LiteralPath $registryKey -Force
    throw 'native_registry_readback_mismatch'
}

[pscustomobject]@{
    Provider = 'Edge'
    Key = $registryKey
    Manifest = $readback
    ExtensionOrigin = $expectedOrigin
    Status = 'registered'
}
