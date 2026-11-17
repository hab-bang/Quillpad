<#
.SYNOPSIS
    Builds, installs and opens the Notepads dev app from the command line, without the Visual Studio IDE.

.DESCRIPTION
    Tasks:
      build      Compiles src/Notepads.sln (Debug, x64) with MSBuild. This also produces an .msix package.
      launch     Installs the last build and opens it. Closes a running dev app first.
      uninstall  Removes the installed dev app.

    Usually called through the Makefile (make build / make run / make dev).

    Requirements:
      - Build Tools for Visual Studio 2022 (or Visual Studio) with the
        "Universal Windows Platform build tools" workload and the Windows 11 SDK (10.0.22621).
      - Windows Developer Mode turned on.

    Debug builds install as "Notepads-Dev" (see src/Notepads/Package.targets), so they run
    side by side with a Store copy of Notepads and keep their own settings.

    Written for Windows PowerShell 5.1, because the Appx cmdlets do not load reliably in PowerShell 7.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build.ps1 build
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('build', 'launch', 'uninstall')]
    [string] $Task
)

$ErrorActionPreference = 'Stop'

$Configuration = 'Debug'
$Platform = 'x64'
$Solution = Join-Path $PSScriptRoot 'src\Notepads.sln'
$PackageDir = Join-Path $PSScriptRoot "src\Notepads\AppPackages\*_${Platform}_${Configuration}_Test"
$LayoutDir = Join-Path $PSScriptRoot "src\Notepads\bin\$Platform\$Configuration\Layout"
$DevPackageName = 'Notepads-Dev' # Set for non-Production builds by src/Notepads/Package.targets
$AppId = 'App'                   # <Application Id> in src/Notepads/Package.appxmanifest

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        # The UWP XAML build targets come only with the UWP workload, so pick an install that has them
        foreach ($installPath in (& $vswhere -all -products * -prerelease -property installationPath)) {
            $msbuild = Join-Path $installPath 'MSBuild\Current\Bin\MSBuild.exe'
            $uwpTargets = Join-Path $installPath 'MSBuild\Microsoft\WindowsXaml'
            if ((Test-Path $msbuild) -and (Test-Path $uwpTargets)) {
                return $msbuild
            }
        }
    }

    throw @"
No MSBuild with the UWP build tools was found. Install them with:

  winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--passive --wait --add Microsoft.VisualStudio.Workload.UniversalBuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended"
"@
}

function Find-MakeAppx {
    $makeappx = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1
    if (-not $makeappx) {
        throw 'makeappx.exe was not found. It comes with the Windows SDK.'
    }
    return $makeappx.FullName
}

function Invoke-Build {
    $msbuild = Find-MSBuild
    Write-Host "Building with $msbuild"

    & $msbuild $Solution /restore /m /nologo /verbosity:minimal `
        "/p:Configuration=$Configuration" "/p:Platform=$Platform" '/p:AppxBundle=Never'

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (MSBuild exit code $LASTEXITCODE)."
    }
}

function Stop-DevApp {
    # CIM, unlike Get-Process, also returns the path of 64-bit processes when this script runs as 32-bit
    # (which happens when a 32-bit make starts it)
    $processes = @(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Notepads.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($LayoutDir, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })
    if ($processes.Count -eq 0) { return }

    Write-Host "Closing the running $DevPackageName"
    $processes | Stop-Process -Force
    # Stop-Process returns before the process has exited and released its files, which the unpack overwrites
    $processes | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
}

# The build output folder is not a complete package layout (registering it crashes the app on start),
# so unpack the .msix that the build produced and install from that instead, as Visual Studio does on F5.
function Expand-DevPackage {
    $package = Get-ChildItem -Path (Join-Path $PackageDir '*.msix') -ErrorAction SilentlyContinue |
        Sort-Object -Property LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $package) {
        throw "No built package found in $PackageDir. Run the build first."
    }

    $makeappx = Find-MakeAppx
    $output = & $makeappx unpack /p $package.FullName /d $LayoutDir /o /l
    if ($LASTEXITCODE -ne 0) {
        throw "Unpacking $($package.Name) failed:`n$($output -join "`n")"
    }
}

# Installs the framework packages the app depends on (.NET Core runtime, VCLibs, ...).
# MSBuild copies them from the Windows SDK next to the package it generates.
function Install-FrameworkDependencies {
    foreach ($dependency in (Get-ChildItem -Path (Join-Path $PackageDir "Dependencies\$Platform\*.appx"))) {
        try {
            Add-AppxPackage -Path $dependency.FullName
        }
        catch {
            # 0x80073D06: the same framework is already installed in a newer version, which is fine
            if ($_.Exception.Message -notmatch '0x80073D06') {
                throw
            }
        }
    }
}

function Register-DevPackage {
    # A registration from another folder (for example an earlier Visual Studio F5) blocks this one
    $installed = Get-AppxPackage -Name $DevPackageName
    if ($installed -and ($installed.InstallLocation -ne $LayoutDir)) {
        Remove-AppxPackage -Package $installed.PackageFullName
    }

    Write-Host "Installing $DevPackageName"
    Add-AppxPackage -Register (Join-Path $LayoutDir 'AppxManifest.xml') -ForceApplicationShutdown
}

function Start-DevApp {
    $package = Get-AppxPackage -Name $DevPackageName
    Write-Host "Opening $DevPackageName"
    Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!$AppId"
}

function Uninstall-DevPackage {
    $package = Get-AppxPackage -Name $DevPackageName
    if ($package) {
        Remove-AppxPackage -Package $package.PackageFullName
        Write-Host "Removed $DevPackageName"
    }
    else {
        Write-Host "$DevPackageName is not installed."
    }
}

switch ($Task) {
    'build' { Invoke-Build }
    'launch' { Stop-DevApp; Expand-DevPackage; Install-FrameworkDependencies; Register-DevPackage; Start-DevApp }
    'uninstall' { Uninstall-DevPackage }
}
