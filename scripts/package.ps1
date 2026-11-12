param([string]$OutputDirectory = '', [string]$CertificateThumbprint = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1')
if ($CertificateThumbprint) { & (Join-Path $PSScriptRoot 'sign.ps1') -CertificateThumbprint $CertificateThumbprint }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$actual = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $root 'out\QabanAlGhad.exe')).Version
if ($actual.ToString(3) -ne $version) { throw 'VERSION does not match the built assembly.' }
$stage = Join-Path $OutputDirectory "QabanAlGhad-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root 'out\QabanAlGhad.exe') $stage
Copy-Item (Join-Path $root 'out\QabanAlGhad.exe.config') $stage
Copy-Item (Join-Path $root 'out\QabanAlGhad.Updater.exe') $stage
Copy-Item (Join-Path $root 'out\QabanAlGhad.Updater.exe.config') $stage
Copy-Item (Join-Path $root 'src\QabanAlGhad\Assets\QabanAlGhad.ico') $stage
Copy-Item (Join-Path $root 'packaging\Start.cmd') $stage
Copy-Item (Join-Path $root 'docs\USER_GUIDE.md') (Join-Path $stage 'README.md')
$hashes = @{}
Get-ChildItem $stage -File | ForEach-Object { $hashes[$_.Name] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
$hashes | ConvertTo-Json | Set-Content (Join-Path $stage 'SHA256.json') -Encoding UTF8
$zip = Join-Path $OutputDirectory "QabanAlGhad-$version.zip"
Compress-Archive -Path $stage -DestinationPath $zip -Force
& (Join-Path $PSScriptRoot 'verify-package.ps1') -Package $zip -Version $version
Write-Host "Public package ready: $zip"
# Never copy settings.json, Returns.setup.json, logs or private device credentials.
