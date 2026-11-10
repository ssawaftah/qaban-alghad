param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'out'
$appRoot = Join-Path $root 'src\QabanAlGhad'
$testRoot = Join-Path $root 'tests\QabanAlGhad.Tests'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $csc)) { throw 'Install Microsoft .NET Framework developer tools before building.' }
New-Item -ItemType Directory -Force $out | Out-Null
$refs = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', '/reference:System.Security.dll')
$app = Join-Path $out 'QabanAlGhad.exe'
$sources = @(Get-ChildItem $appRoot -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } | ForEach-Object { $_.FullName })
& $csc /nologo /codepage:65001 /warnaserror+ /target:winexe /platform:anycpu "/win32icon:$appRoot\Assets\QabanAlGhad.ico" "/out:$app" @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item (Join-Path $appRoot 'App.config') "$app.config" -Force
$tests = Join-Path $out 'QabanAlGhad.Tests.exe'
$testSources = @(Get-ChildItem $testRoot -Filter *.cs | ForEach-Object { $_.FullName })
& $csc /nologo /codepage:65001 /warnaserror+ /target:exe "/out:$tests" "/reference:$app" @refs @testSources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Copy-Item (Join-Path $appRoot 'App.config') "$tests.config" -Force
if (!$SkipTests) {
    # HttpListener integration has a separate job because Windows may need a URL ACL.
    & $tests --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Protocol/compatibility tests failed.' }
    & $tests --dual-test
    if ($LASTEXITCODE -ne 0) { throw 'Independent-delivery tests failed.' }
}
Write-Host "Build ready: $app"
