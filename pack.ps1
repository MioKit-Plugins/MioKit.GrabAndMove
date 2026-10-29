[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    [string]$PackageId = "MioKit.GrabAndMove",
    [string]$Configuration = "Release",
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "artifacts"
}

$projectPath = Join-Path $PSScriptRoot "plugin\MioKit.GrabAndMove.csproj"
$manifestPath = Join-Path $PSScriptRoot "plugin\plugin.json"
$forbiddenFields = @("pluginVersion", "releaseState", "releaseDate")
$forbiddenDlls = @("MioKit.Sdk.dll", "MioKit.SourceGenerate.dll", "MioKit.Webview2.dll", "MioKit.Runtime.dll", "MioKit.Framework.dll")

if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?(?:\+[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "PackageVersion must be a valid NuGet SemVer, for example 1.2.0 or 1.2.0-beta.1."
}
if (-not (Test-Path -LiteralPath $projectPath) -or -not (Test-Path -LiteralPath $manifestPath)) {
    throw "The plugin project or plugin.json was not found."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.metadataVersion -ne "1.0" -or [string]::IsNullOrWhiteSpace($manifest.id) -or
    [string]::IsNullOrWhiteSpace($manifest.name) -or [string]::IsNullOrWhiteSpace($manifest.assembly) -or
    [string]::IsNullOrWhiteSpace($manifest.minSdkVersion)) {
    throw "plugin.json must use metadataVersion 1.0 and contain id, name, assembly, and minSdkVersion."
}
foreach ($field in $forbiddenFields) {
    if ($manifest.PSObject.Properties.Name -contains $field) {
        throw "plugin.json contains forbidden field '$field'. Version and prerelease state are controlled by PackageVersion."
    }
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
& dotnet build $projectPath -c $Configuration -p:PackageId=$PackageId -p:PackageVersion=$PackageVersion --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
& dotnet pack $projectPath -c $Configuration --no-build -o $OutputPath -p:PackageId=$PackageId -p:PackageVersion=$PackageVersion --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }

$packagePath = Join-Path $OutputPath "$PackageId.$PackageVersion.nupkg"
if (-not (Test-Path -LiteralPath $packagePath)) { throw "The generated package was not found: $packagePath" }
$inspectPath = Join-Path ([IO.Path]::GetTempPath()) ("miokit-pack-" + [guid]::NewGuid().ToString("N"))
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($packagePath, $inspectPath)
    $packagedManifestPath = Join-Path $inspectPath "plugin.json"
    if (-not (Test-Path -LiteralPath $packagedManifestPath)) { throw "The nupkg root does not contain plugin.json." }
    if (-not (Test-Path -LiteralPath (Join-Path $inspectPath $manifest.assembly))) { throw "The nupkg root does not contain the entry assembly: $($manifest.assembly)" }
    if ($manifest.icon -and -not (Test-Path -LiteralPath (Join-Path $inspectPath $manifest.icon))) { throw "The nupkg does not contain the resource declared by plugin.json: $($manifest.icon)" }
    foreach ($dll in $forbiddenDlls) {
        if (Get-ChildItem -LiteralPath $inspectPath -Recurse -File -Filter $dll) { throw "The nupkg must not contain host DLL: $dll" }
    }

    $nuspec = Get-ChildItem -LiteralPath $inspectPath -Filter *.nuspec -File | Select-Object -First 1
    if (-not $nuspec) { throw "The nupkg does not contain a nuspec file." }
    [xml]$nuspecXml = Get-Content -LiteralPath $nuspec.FullName -Raw -Encoding UTF8
    $description = [string]$nuspecXml.package.metadata.description
    $packageIcon = ([string]$nuspecXml.package.metadata.icon).Replace('\', '/')
    $manifestIcon = ([string]$manifest.icon).Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($packageIcon) -or $packageIcon -cne $manifestIcon) {
        throw "The nuspec icon '$packageIcon' does not match plugin.json icon '$manifestIcon'."
    }
    if ($description -match 'miokit-plugin-manifest:v1|<!--') { throw "The nuspec description still uses the legacy HTML comment format." }
    $envelope = $description | ConvertFrom-Json
    if ($envelope.schema -ne "miokit.plugin-package" -or $envelope.schemaVersion -ne 1) {
        throw "The nuspec description is not a miokit.plugin-package v1 JSON envelope."
    }

    $packagedManifest = Get-Content -LiteralPath $packagedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifestJson = $packagedManifest | ConvertTo-Json -Depth 100 -Compress
    $envelopePluginJson = $envelope.plugin | ConvertTo-Json -Depth 100 -Compress
    if ($envelopePluginJson -cne $manifestJson) { throw "The plugin in the nuspec JSON envelope does not match the packaged plugin.json." }
    foreach ($field in $forbiddenFields) {
        if ($envelope.plugin.PSObject.Properties.Name -contains $field) { throw "The nuspec JSON envelope contains forbidden field '$field'." }
    }
}
finally {
    if (Test-Path -LiteralPath $inspectPath) { Remove-Item -LiteralPath $inspectPath -Recurse -Force }
}

Write-Host "Package created: $packagePath"
