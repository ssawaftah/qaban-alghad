param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$TimestampServer = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$thumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
if ($thumbprint -notmatch '^[A-F0-9]{40}$') { throw 'Supply the SHA-1 thumbprint of an installed code-signing certificate.' }
$timestamp = [Uri]$TimestampServer
if ($timestamp.Scheme -ne 'https' -or $timestamp.UserInfo) { throw 'Use an HTTPS timestamp service.' }
$certificate = Get-Item "Cert:\CurrentUser\My\$thumbprint" -ErrorAction Stop
if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'A current code-signing certificate with its private key is required.' }
$tool = Get-Command signtool.exe -ErrorAction Stop
foreach ($name in @('QabanAlGhad.exe', 'QabanAlGhad.Updater.exe')) {
    $path = Join-Path $root "out\$name"
    & $tool.Source sign /sha1 $thumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $name" }
    & $tool.Source verify /pa /all $path
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $name" }
    $signature = Get-AuthenticodeSignature $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $thumbprint) {
        throw "Unexpected signer or untrusted signature: $name"
    }
}
# Certificates are provisioned by the runner owner; no private keys or passwords enter source control.
