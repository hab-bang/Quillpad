<#
.SYNOPSIS
    Installs or updates Quillpad from an unzipped Quillpad_<version>_install.zip.

.DESCRIPTION
    Replaces the Install.ps1 / Add-AppDevPackage.ps1 that Visual Studio generates. That script passes
    every bundled framework package (VCLibs, .NET Native) to Add-AppxPackage together with the app,
    with -ForceApplicationShutdown. Windows then services each framework again even when the same
    version is already installed, and force-closes every app that uses it (Snipping Tool, the
    Microsoft Store, Widgets, ...). If that servicing fails half way, the framework stays hidden
    from the user and all those apps stop starting ("Check the Store for more info about ...").

    This script instead:
      1. Trusts the Quillpad signing certificate (asks for admin rights once, only if needed).
      2. Installs a bundled framework only if it is missing or older than the bundled one, and
         never force-closes apps for it.
      3. Installs Quillpad with -ForceTargetApplicationShutdown, which closes only Quillpad itself.

    Expected layout, next to this script:
      Quillpad_<version>.msixbundle
      Quillpad_<version>.cer
      Dependencies\<x64|arm64|...>\*.appx

    Written for Windows PowerShell 5.1, because the Appx cmdlets do not load reliably in
    PowerShell 7. Under PowerShell 7 it starts itself again in Windows PowerShell.

.PARAMETER NoPrompt
    Do not wait for Enter before closing. For scripted installs.
#>
[CmdletBinding()]
param(
    [switch] $NoPrompt,
    # Internal: set when the script runs itself elevated to trust the certificate.
    [string] $TrustCertificate
)

$ErrorActionPreference = 'Stop'

function Exit-Script([int] $Code) {
    if (-not $NoPrompt) {
        Read-Host 'Press Enter to close' | Out-Null
    }
    exit $Code
}

# Elevated step: only trust the certificate, then return to the caller.
if ($TrustCertificate) {
    Import-Certificate -FilePath $TrustCertificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    exit 0
}

if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($NoPrompt) { $arguments += '-NoPrompt' }
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @arguments
    exit $LASTEXITCODE
}

function Get-PackageArchitecture {
    # PROCESSOR_ARCHITEW6432 is set when this runs as a 32-bit process on 64-bit Windows
    $machine = $env:PROCESSOR_ARCHITEW6432
    if (-not $machine) { $machine = $env:PROCESSOR_ARCHITECTURE }
    switch ($machine) {
        'AMD64' { return 'x64' }
        'ARM64' { return 'arm64' }
        default { throw "Quillpad is built for x64 and ARM64 only. This PC is $machine." }
    }
}

function Get-PackageIdentity([string] $Path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (-not $entry) { throw "$Path has no AppxManifest.xml." }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally {
        $zip.Dispose()
    }
    $identity = $manifest.Package.Identity
    return [pscustomobject]@{
        Name         = $identity.Name
        Version      = [version] $identity.Version
        Architecture = $identity.ProcessorArchitecture
    }
}

function Install-Certificate([string] $CertificatePath) {
    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
    if (Test-Path "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)") {
        Write-Host 'The Quillpad certificate is already trusted.'
        return
    }

    Write-Host 'Windows does not trust the Quillpad certificate yet. Asking for admin rights to add it (first install only).'
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -TrustCertificate `"$CertificatePath`""
    try {
        $process = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    }
    catch {
        throw 'Admin rights were refused, so the certificate was not added. Quillpad cannot be installed without it.'
    }
    if ($process.ExitCode -ne 0 -or -not (Test-Path "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)")) {
        throw 'Adding the Quillpad certificate failed.'
    }
    Write-Host 'Added the Quillpad certificate.'
}

function Install-Frameworks([string] $Architecture) {
    $folder = Join-Path $PSScriptRoot 'Dependencies'
    $candidates = @(Get-ChildItem -Path (Join-Path $folder '*.appx'), (Join-Path $folder '*.msix'), `
            (Join-Path $folder "$Architecture\*.appx"), (Join-Path $folder "$Architecture\*.msix") -File -ErrorAction SilentlyContinue)

    foreach ($file in $candidates) {
        $bundled = Get-PackageIdentity $file.FullName
        $installed = @(Get-AppxPackage -Name $bundled.Name |
            Where-Object { $_.Architecture.ToString() -eq $bundled.Architecture -and [version] $_.Version -ge $bundled.Version })
        if ($installed.Count -gt 0) {
            Write-Host "Already installed: $($bundled.Name) $($installed[0].Version) ($($bundled.Architecture))"
            continue
        }

        Write-Host "Installing $($bundled.Name) $($bundled.Version) ($($bundled.Architecture))"
        try {
            # No -ForceApplicationShutdown: a framework is shared by many apps, and none of them should be closed for it
            Add-AppxPackage -Path $file.FullName
        }
        catch {
            # 0x80073D06: a newer version is installed for another user or staged, which is fine
            if ($_.Exception.Message -notmatch '0x80073D06') { throw }
        }
    }
}

try {
    $bundle = @(Get-ChildItem -Path (Join-Path $PSScriptRoot '*.msixbundle') -File)
    $certificate = @(Get-ChildItem -Path (Join-Path $PSScriptRoot '*.cer') -File)
    if ($bundle.Count -ne 1 -or $certificate.Count -ne 1) {
        throw "Expected one .msixbundle and one .cer next to this script, found $($bundle.Count) and $($certificate.Count). Unzip the whole Quillpad_<version>_install.zip and run Install.ps1 from there."
    }

    Install-Certificate $certificate[0].FullName
    Install-Frameworks (Get-PackageArchitecture)

    Write-Host "Installing $($bundle[0].Name)"
    # Closes a running Quillpad, and nothing else
    Add-AppxPackage -Path $bundle[0].FullName -ForceTargetApplicationShutdown

    $app = Get-AppxPackage -Name Quillpad
    Write-Host "Quillpad $($app.Version) is installed. Open it from the Start menu." -ForegroundColor Green
    Exit-Script 0
}
catch {
    Write-Host "Installing Quillpad failed: $($_.Exception.Message)" -ForegroundColor Red
    Exit-Script 1
}
