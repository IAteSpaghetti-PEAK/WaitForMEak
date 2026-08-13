# Builds the mod and stages everything a release needs.
#
# Produces:
#   artifacts\WaitForMEak-<version>.zip                  a Thunderstore package you can upload by hand
#   release-assets\WaitForMEak-<version>-thunderstore.zip
#   release-assets\WaitForMEak.dll
#
# release-assets is what .github\workflows\release.yml attaches to the GitHub
# release and hands to tcli, and it has to be committed, because the workflow
# can't build the mod itself (no game DLLs on a runner). So: run this, commit
# release-assets, then push the tag.
#
# Usage: powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 [-SkipDeploy]
#   -SkipDeploy: don't copy the built DLL into the game's BepInEx\plugins folder.
param([switch]$SkipDeploy)
$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot

# Read version from the manifest so it stays the single source of truth
$manifest = Get-Content (Join-Path $projectDir "thunderstore\manifest.json") -Raw | ConvertFrom-Json
$version = $manifest.version_number
$name = $manifest.name

$icon = Join-Path $projectDir "thunderstore\icon.png"
if (-not (Test-Path $icon)) {
    throw "thunderstore\icon.png is missing. Thunderstore requires a 256x256 PNG."
}
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($icon)
$iconWidth = $img.Width; $iconHeight = $img.Height
$img.Dispose()
if ($iconWidth -ne 256 -or $iconHeight -ne 256) {
    throw "thunderstore\icon.png is ${iconWidth}x${iconHeight}. Thunderstore requires exactly 256x256."
}

Write-Host "Building $name $version..."
$buildArgs = @((Join-Path $projectDir "WaitForMEak.csproj"), "-c", "Release")
if ($SkipDeploy) { $buildArgs += "-p:SkipDeploy=true"; Write-Host "(not deploying to the game's plugins folder)" }
dotnet build @buildArgs | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = Join-Path $projectDir "bin\Release\netstandard2.1\WaitForMEak.dll"
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }

# Stage the package: manifest + icon + README + CHANGELOG at root, DLL in plugins/
$stage = Join-Path $env:TEMP "wfm_ts_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory "$stage\plugins" -Force | Out-Null
Copy-Item (Join-Path $projectDir "thunderstore\manifest.json") $stage
Copy-Item $icon $stage
Copy-Item (Join-Path $projectDir "thunderstore\CHANGELOG.md") $stage
Copy-Item (Join-Path $projectDir "README.md") $stage
Copy-Item $dll "$stage\plugins"

$artifacts = Join-Path $projectDir "artifacts"
New-Item -ItemType Directory $artifacts -Force | Out-Null
$zip = Join-Path $artifacts "$name-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force

# Stage the same package where the release workflow looks for it, plus the bare
# DLL for people who install by hand.
$releaseAssets = Join-Path $projectDir "release-assets"
New-Item -ItemType Directory $releaseAssets -Force | Out-Null
Get-ChildItem $releaseAssets -File | Remove-Item -Force
Copy-Item $zip (Join-Path $releaseAssets "$name-$version-thunderstore.zip")
Copy-Item $dll $releaseAssets

Write-Host ""
Write-Host "Packaged: $zip"
Write-Host "Staged:   release-assets\$name-$version-thunderstore.zip"
Write-Host "          release-assets\$name.dll"
Write-Host ""
Write-Host "Next: commit release-assets, then push tag v$version to publish."
