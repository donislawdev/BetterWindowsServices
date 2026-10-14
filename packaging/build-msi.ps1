#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build the Windows installer of one release from its two signed packages.

.DESCRIPTION
    One installer carries the window and the command line, for every account on the machine, with the
    command line on the machine's PATH - the owner's decision of 2026-10-14, beside the two zips, which
    stay as they are. It is built from the SIGNED packages, because the signature is on the programs
    inside them: an installer made before the card signed them would carry unsigned copies. So a release
    builds it in phase B (packaging/sign-release.ps1), on the machine with the card, and never in
    release.yml. The same script builds an unsigned one from dist/staging on a Windows runner
    (.github/workflows/installer.yml), so that what the installer DOES is asked on a clean machine.

    What it lays down is the two packages and nothing else: both executables, LICENSE and
    THIRD-PARTY-NOTICES.md. A file both packages carry goes in once, and only when both carry the same
    bytes - two different copies of one file are a question about how the release was built, and the
    installer does not pick one. One name to Windows is one file, so names are compared without case.

    Every value comes from one place: the names and addresses from packaging/components.json (its
    `product`, its `packages` and its `installer`), the version from the tag, the template from
    packaging/msi/. The built database is then asked what it carries - no custom action, no binary of
    WiX's own, the version, the upgrade code, the Restart Manager setting and the number of files - so a
    template that grew an extension is refused here and not found on somebody's machine.

    Modes:
      -Window <dir> -CommandLine <dir> -OutDir <dir>   build, written whole or not at all
      -Check        whether this machine can build it (the template, the icon, WiX), so sign-release.ps1
                    can refuse before the card signs anything
      -SourceOnly   print the rendered installer source and build nothing (the guards read it)
      -ProductName  print the name the installer carries, which its signature names

    Exit codes: 0 done, 1 refused - one line on stderr says which input and why. Nothing here signs or
    publishes anything. Call it as its own process (pwsh -File): a refusal ends the process it runs in.

.PARAMETER Tag
    The release, e.g. v0.5.0. A tag with a hyphen is a release candidate and gets no installer.

.PARAMETER Window
    The window package as it unzips: the folder holding BetterWindowsServices.exe.

.PARAMETER CommandLine
    The command line package as it unzips: the folder holding bws.exe.

.PARAMETER OutDir
    An existing folder the installer is moved into once it is built and checked.
#>
[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(ParameterSetName = 'Build', Mandatory)] [string] $Window,
    [Parameter(ParameterSetName = 'Build', Mandatory)] [string] $CommandLine,
    [Parameter(ParameterSetName = 'Build', Mandatory)] [string] $OutDir,
    [Parameter(ParameterSetName = 'Check', Mandatory)] [switch] $Check,
    [Parameter(ParameterSetName = 'Source', Mandatory)] [switch] $SourceOnly,
    [Parameter(ParameterSetName = 'Name', Mandatory)] [switch] $ProductName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
# A .wxs WiX cannot read as it stands - every doubled-brace placeholder is filled first. Named .wxs so the
# sweeps that read every published file of the repository read its comments too.
$templatePath = Join-Path $PSScriptRoot 'msi/BetterWindowsServices.wxs'
$registerPath = Join-Path $PSScriptRoot 'components.json'
$iconPath = Join-Path $root 'src/bws.ico'

# A refusal is one plain line on stderr and exit 1. Not a thrown exception: PowerShell's error view wraps a
# long message at the console width and frames it with the line of code, so the sentence a person or a
# guard reads would be broken in two (measured in the owner's Chrono Mock, the first run of its guard).
# `exit` from here still runs the `finally` that removes the staging folder.
function Stop-Build([string] $message) {
    [Console]::Error.WriteLine("build-msi: $message")
    exit 1
}

if (-not (Test-Path -LiteralPath $registerPath)) { Stop-Build "missing the register at $registerPath" }
$register = Get-Content -Raw -LiteralPath $registerPath | ConvertFrom-Json
if (-not $register.PSObject.Properties['installer']) {
    Stop-Build 'packaging/components.json names no installer, so there is nothing to build'
}

# The file a person downloads, named where every published file is named. Not versioned, like the two
# zips: the address it is downloaded from carries the tag already, and a name that moved every release
# would have to be learned by every check that asks for it. Public for good - a feed will point at it.
$InstallerName = [string]$register.installer.file
if ($InstallerName -notmatch '^[A-Za-z0-9.-]+\.msi$') {
    Stop-Build "packaging/components.json names the installer '$InstallerName', which is not a file name ending in .msi"
}

# The product, not a build, and it never changes - see the comment on it in the template. Generated once on
# 2026-10-14 and pinned by tests/Bws.Architecture.Tests/InstallerGuards.cs, which keeps a second copy that
# does not move with this one.
$UpgradeCode = 'FD038501-7A2F-49C9-A587-E8DF3EFB9718'

# The WiX this project builds with, the owner's decision of 2026-10-14. Another version builds another
# package from the same source, so a different one is refused rather than used. 5.0.2 is the last release
# before the Open Source Maintenance Fee agreement that comes with WiX 6 and later, and the version the
# owner's two other projects build with. Its last release was 2024-10-05 - docs/02, ADR-29.
$WixVersion = '5.0.2'

# The two sentences a person reads from the installer itself: the shortcut's tooltip, and the refusal to
# go back a version. [ProductName] is filled in by Windows Installer.
$ShortcutDescription = 'A services.msc replacement with search, dry runs, and a diff of what changed'
$DowngradeMessage = 'A newer version of [ProductName] is already installed. Uninstall it first to install this one.'

# The executables, from the register, and the two documents every package carries beside its executable
# (build-dist.ps1, $Alongside). A package without them is not the package a release builds.
$WindowExe = [string]$register.packages.gui.executable
$CommandLineExe = [string]$register.packages.cli.executable
$Documents = @('LICENSE', 'THIRD-PARTY-NOTICES.md')

# The version a tag names, or a refusal saying why it gets no installer.
function Get-InstallerVersion([string] $releaseTag) {
    if ($releaseTag.Contains('-')) {
        Stop-Build ("$releaseTag is a release candidate, and candidates get no installer. Windows Installer reads " +
            "only the three numbers, so it would take $($releaseTag.Split('-')[0]) for the release itself and the " +
            'release would not replace it on a machine that took the candidate')
    }
    if ($releaseTag -notmatch '^v(\d+)\.(\d+)\.(\d+)$') {
        Stop-Build "'$releaseTag' is not a release tag. Pass it the way the release is tagged, for example v0.5.0"
    }
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3]
    if ($major -gt 255 -or $minor -gt 255 -or $patch -gt 65535) {
        Stop-Build ("$releaseTag does not fit an installer version, which holds at most 255 for the first two " +
            'numbers and 65535 for the third')
    }
    return "$major.$minor.$patch"
}

# Every placeholder the template may use, from the one place each value lives.
function Get-TemplateValues([string] $version, [string] $releaseTag) {
    $product = $register.product
    # The register names the supplier the way SPDX does ("Person: X" or "Organization: X"). The installer
    # shows only the name.
    $publisher = ($product.supplier -split ':\s*', 2)[-1]
    foreach ($field in @($product.name, $publisher, $product.homepage, $product.source, $WindowExe, $CommandLineExe)) {
        if (-not $field) { Stop-Build 'packaging/components.json is missing a field the installer names' }
    }
    return [ordered]@{
        APP_NAME             = $product.name
        PUBLISHER            = $publisher
        VERSION              = $version
        UPGRADE_CODE         = $UpgradeCode
        PROJECT_URL          = $product.homepage
        ISSUES_URL           = "$($product.source)/issues"
        RELEASE_NOTES_URL    = "$($product.source)/releases/tag/$releaseTag"
        WINDOW_EXE           = $WindowExe
        SHORTCUT_DESCRIPTION = $ShortcutDescription
        DOWNGRADE_MESSAGE    = $DowngradeMessage
    }
}

# The installer source for one release, every placeholder filled - and a template that asks for a value
# nobody gives, or no longer asks for one that is given, is refused rather than built half right.
function Get-InstallerSource([string] $version, [string] $releaseTag) {
    if (-not (Test-Path -LiteralPath $templatePath)) {
        Stop-Build "there is no template at $templatePath. A tree from before the installer existed has none"
    }
    $text = [System.IO.File]::ReadAllText($templatePath)
    $values = Get-TemplateValues $version $releaseTag
    $asked = @([regex]::Matches($text, '\{\{([A-Z_]+)\}\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $unknown = @($asked | Where-Object { -not $values.Contains($_) })
    if ($unknown) { Stop-Build "the template asks for $($unknown -join ', '), which build-msi.ps1 does not give" }
    $unused = @($values.Keys | Where-Object { $asked -notcontains $_ })
    if ($unused) { Stop-Build "the template uses no $($unused -join ', ') any more - take it out of build-msi.ps1" }
    foreach ($key in $values.Keys) {
        # Escaped for an XML attribute, which is where every placeholder stands.
        $text = $text.Replace("{{$key}}", [System.Security.SecurityElement]::Escape([string]$values[$key]))
    }
    return $text
}

# The wix command at the pinned version, or a refusal with the command that installs it.
function Find-Wix {
    $install = "    dotnet tool install --global wix --version $WixVersion`n" +
        'It runs on .NET - install the .NET SDK first if there is no dotnet command.'
    # Where `dotnet tool install --global` puts it when that folder is not on PATH. From the environment
    # rather than $HOME, which PowerShell takes from the account and not from USERPROFILE.
    $found = (Get-Command wix -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1)
    $path = if ($found) { $found.Source } elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet\tools\wix.exe' } else { '' }
    if (-not $path -or -not (Test-Path -LiteralPath $path)) {
        Stop-Build "WiX is not installed, and the installer is built with it. Install the version this project builds with:`n$install"
    }
    $said = (& $path --version 2>&1 | Out-String).Trim()
    $exitCode = $LASTEXITCODE
    $reported = ($said -split '\+', 2)[0]
    if ($exitCode -ne 0 -or $reported -ne $WixVersion) {
        Stop-Build ("$path says it is version '$reported', and the installer is built with $WixVersion - another " +
            "version builds another package from the same source. Replace it:`n    dotnet tool uninstall --global wix`n$install")
    }
    return $path
}

# One package as it unzips, refused when it is not the package a release builds.
function Assert-Package([string] $folder, [string] $what, [string] $executable) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
        Stop-Build "-$what $folder is not a folder. Pass the folder that holds $executable"
    }
    foreach ($relative in @($executable) + $Documents) {
        if (-not (Test-Path -LiteralPath (Join-Path $folder $relative) -PathType Leaf)) {
            Stop-Build "$folder holds no $relative, so it is not the package a release builds"
        }
    }
}

# Both packages laid out the way the installer puts them down - all of it, or a refusal. Returns the number
# of files staged.
# Enumerated literally, hidden files included. `Copy-Item -Path <folder>\*` reads the folder as a pattern: a
# package in a folder whose name holds brackets copied NOTHING and said nothing, and a hidden file at the
# top was left out (measured in Chrono Mock, 2026-10-07). The database check counts what was staged, so an
# incomplete staging would have built and passed - hence the check right after the copy.
function New-StagedPayload([string[]] $folders, [string] $into) {
    $cameFrom = @{}
    foreach ($from in $folders) {
        $source = (Get-Item -LiteralPath $from).FullName.TrimEnd('\', '/')
        foreach ($file in @(Get-ChildItem -LiteralPath $source -Recurse -File -Force)) {
            $relative = $file.FullName.Substring($source.Length + 1)
            $key = $relative.ToUpperInvariant()
            $target = Join-Path $into $relative
            if ($cameFrom.ContainsKey($key)) {
                $first = $cameFrom[$key]
                if ((Get-FileHash -LiteralPath $first.Path).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
                    Stop-Build ("$($first.Folder) holds $($first.Relative) and $source holds $relative, one file on " +
                        'Windows, and not the same bytes. One installer carries one copy - look at how the release ' +
                        'built the two packages')
                }
                continue
            }
            $cameFrom[$key] = @{ Path = $file.FullName; Folder = $source; Relative = $relative }
            $parent = Split-Path -Parent $target
            if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }
    $staged = @(Get-ChildItem -LiteralPath $into -Recurse -File -Force).Count
    if ($staged -ne $cameFrom.Count) {
        Stop-Build "staging holds $staged file(s) and the two packages $($cameFrom.Count) - nothing was built"
    }
    return $staged
}

# The rows one query returns from an installer database, each written to the pipeline as one array of its
# fields, so a caller collects them with @(). A table the database does not have answers with no rows,
# which is the answer for "no custom action" as well.
# Every COM call that returns nothing is cast to [void] - uncast, Execute and Close each wrote a $null to
# the pipeline (measured in Chrono Mock). And every COM object is released, the records too: a view or a
# record left to the garbage collector keeps the database open, and the installer could not be moved out of
# staging ("being used by another process", measured there after every check had passed).
function Read-MsiRows([string] $msi, [string] $query, [int] $fields) {
    $installer = $null
    $database = $null
    $view = $null
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($msi, 0)
        try {
            $view = $database.OpenView($query)
        }
        catch {
            return
        }
        [void]$view.Execute()
        while ($true) {
            $record = $view.Fetch()
            if ($null -eq $record) { break }
            try {
                $row = [string[]]::new($fields)
                for ($i = 0; $i -lt $fields; $i++) { $row[$i] = $record.StringData($i + 1) }
            }
            finally {
                [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($record)
            }
            Write-Output -NoEnumerate $row
        }
        [void]$view.Close()
    }
    finally {
        foreach ($held in @($view, $database, $installer)) {
            if ($null -ne $held) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($held) }
        }
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
    }
}

# The built database, asked what it carries - before it is moved where anybody would find it.
function Assert-Database([string] $msi, [string] $version, [int] $fileCount) {
    $properties = @{}
    foreach ($row in @(Read-MsiRows $msi 'SELECT `Property`, `Value` FROM `Property`' 2)) { $properties[$row[0]] = $row[1] }
    if ($properties.Count -lt 5) {
        Stop-Build "read $($properties.Count) properties out of the built installer, so this check is not reading it"
    }
    $expected = [ordered]@{
        ProductVersion           = $version
        UpgradeCode              = "{$UpgradeCode}"
        ALLUSERS                 = '1'
        MSIRESTARTMANAGERCONTROL = 'Disable'
        ARPNOREPAIR              = '1'
        ARPNOMODIFY              = '1'
    }
    foreach ($name in $expected.Keys) {
        if ($properties[$name] -ne $expected[$name]) {
            Stop-Build "the built installer says $name = '$($properties[$name])', and it has to say '$($expected[$name])'"
        }
    }
    $actions = @(Read-MsiRows $msi 'SELECT `Action` FROM `CustomAction`' 1)
    if ($actions.Count -gt 0) {
        Stop-Build "the built installer carries custom actions ($(($actions | ForEach-Object { $_[0] }) -join ', ')). It runs no code of its own or of WiX's"
    }
    $binaries = @(Read-MsiRows $msi 'SELECT `Name` FROM `Binary`' 1)
    if ($binaries.Count -gt 0) {
        Stop-Build "the built installer embeds binaries ($(($binaries | ForEach-Object { $_[0] }) -join ', ')). It carries our files and nothing of WiX's"
    }
    $files = @(Read-MsiRows $msi 'SELECT `File` FROM `File`' 1)
    if ($files.Count -ne $fileCount) {
        Stop-Build "the built installer lays down $($files.Count) files and the two packages hold $fileCount"
    }
    $shortcuts = @(Read-MsiRows $msi 'SELECT `Shortcut` FROM `Shortcut`' 1)
    $paths = @(Read-MsiRows $msi 'SELECT `Name` FROM `Environment`' 1)
    if ($shortcuts.Count -ne 1 -or $paths.Count -ne 1) {
        Stop-Build "the built installer has $($shortcuts.Count) shortcut(s) and $($paths.Count) environment entr(y/ies), and it has one of each"
    }
}

# --- the modes --------------------------------------------------------------------------------------

$version = Get-InstallerVersion $Tag

if ($SourceOnly) {
    [Console]::Out.Write((Get-InstallerSource $version $Tag))
    return
}
if ($ProductName) {
    Write-Output (Get-TemplateValues $version $Tag).APP_NAME
    return
}

$source = Get-InstallerSource $version $Tag
if (-not (Test-Path -LiteralPath $iconPath)) { Stop-Build "the icon $iconPath is not in the tree" }

if ($Check) {
    Write-Output "ready to build $InstallerName $version with WiX $WixVersion ($(Find-Wix))"
    return
}

# Every refusal about the inputs comes before WiX is looked for, so the guards can ask each of them on a
# machine without WiX, and a run with WiX missing stops at the same place on every system.
Assert-Package $Window 'Window' $WindowExe
Assert-Package $CommandLine 'CommandLine' $CommandLineExe
if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) { Stop-Build "-OutDir $OutDir is not a folder. Pass one that exists" }
$target = Join-Path $OutDir $InstallerName
if (Test-Path -LiteralPath $target) {
    Stop-Build "$target is already there. Nothing is overwritten - remove it or pass another -OutDir"
}

# Beside nothing of the caller's: sign-release.ps1 hands its own folder of files to publish as -OutDir, and
# a folder left inside it would be published, or would break the checksums written over it.
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('bws-msi-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $staged = Join-Path $staging 'payload'
    New-Item -ItemType Directory -Path $staged | Out-Null
    $fileCount = New-StagedPayload @($Window, $CommandLine) $staged
    $wix = Find-Wix

    $wxs = Join-Path $staging 'BetterWindowsServices.wxs'
    [System.IO.File]::WriteAllText($wxs, $source, (New-Object System.Text.UTF8Encoding($false)))
    $built = Join-Path $staging $InstallerName
    $arguments = @('build', $wxs, '-arch', 'x64', '-pdbtype', 'none',
        '-d', "PayloadDir=$staged", '-d', "IconFile=$iconPath", '-o', $built)
    Write-Host "    `$ wix $($arguments -join ' ')"
    & $wix @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $built)) {
        Stop-Build "wix build exited $LASTEXITCODE, and nothing was written to $OutDir. What it said is above"
    }
    Assert-Database $built $version $fileCount
    # Moved in only once it is whole and checked, so a run that fails or is stopped leaves no half written
    # installer where the caller would find it.
    Move-Item -LiteralPath $built -Destination $target
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    # Said rather than swallowed: a staging folder that could not go keeps a copy of both programs (about
    # 270 MB) in the temp folder, and nobody would know it was there.
    if (Test-Path -LiteralPath $staging) {
        Write-Warning "build-msi: could not remove the staging folder $staging - delete it by hand"
    }
}
Write-Output $target
