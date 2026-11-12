param([Parameter(Mandatory=$true)][string]$Package, [Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$allowed = @('QabanAlGhad.exe', 'QabanAlGhad.exe.config', 'QabanAlGhad.Updater.exe', 'QabanAlGhad.Updater.exe.config', 'QabanAlGhad.ico', 'Start.cmd', 'README.md', 'SHA256.json')
$prefix = "QabanAlGhad-$Version/"
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package).Path)
try {
    $seen = @{}
    $manifest = $null
    $actualHashes = @{}
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if ($name -eq $prefix -and $entry.Length -eq 0) { continue }
        if (!$name.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'Invalid package root.' }
        $name = $name.Substring($prefix.Length)
        if ($allowed -notcontains $name -or $seen.ContainsKey($name)) { throw "Unexpected, private, or duplicate package entry: $name" }
        $seen[$name] = $true
        $stream = $entry.Open()
        try {
            if ($name -eq 'SHA256.json') {
                $reader = New-Object IO.StreamReader($stream)
                try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            } else {
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $actualHashes[$name] = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
            }
        } finally { $stream.Dispose() }
    }
    if ($seen.Count -ne $allowed.Count -or $null -eq $manifest) { throw 'Incomplete public package.' }
    if (@($manifest.PSObject.Properties).Count -ne $actualHashes.Count) { throw 'Invalid package hash manifest.' }
    foreach ($name in $actualHashes.Keys) {
        if ($manifest.PSObject.Properties.Name -notcontains $name -or $manifest.$name -cne $actualHashes[$name]) { throw "Package hash mismatch: $name" }
    }
} finally { $archive.Dispose() }
Write-Host 'Public package file list and hashes verified; no settings, backups, setup files, or logs.'
