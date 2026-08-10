param(
    [string]$Version,
    [string]$Platform,
    [string]$Arch
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($Version) -or [string]::IsNullOrEmpty($Platform) -or [string]::IsNullOrEmpty($Arch)) {
    Write-Host '{"isSuccess":false,"message":"Invocation error!"}'
    exit 1
}

switch ($Platform) {
    "darwin" { $Platform = "osx" }
    "win32" { $Platform = "win" }
}

$RuntimeId = "$Platform-$Arch"
$SdkDir = Join-Path (Split-Path -Parent $PSScriptRoot) "Sdk"
$ArchiveName = "dotnet-sdk-$Version-$RuntimeId.zip"
$DownloadUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$Version/$ArchiveName"
$ArchivePath = Join-Path $PSScriptRoot $ArchiveName

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $ArchivePath -UseBasicParsing
}
catch {
    Write-Host ('{"isSuccess":false,"message":"Failed to download ' + $DownloadUrl + '"}')
    exit 1
}

if (Test-Path $SdkDir) {
    Remove-Item $SdkDir -Recurse -Force
}
New-Item -ItemType Directory -Path $SdkDir | Out-Null

try {
    Expand-Archive -Path $ArchivePath -DestinationPath $SdkDir -Force
}
catch {
    Write-Host ('{"isSuccess":false,"message":"Failed to extract ' + $ArchivePath.Replace('\', '\\') + '"}')
    Remove-Item $ArchivePath -Force
    exit 1
}

Remove-Item $ArchivePath -Force
Write-Host '{"isSuccess":true,"message":null}'
exit 0
