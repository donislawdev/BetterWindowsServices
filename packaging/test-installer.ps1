<#
.SYNOPSIS
    Ask THIS machine what the Windows installer does, and say what each step left behind.

.DESCRIPTION
    What the installer does on a machine is not in its source, so the guards in
    tests/Bws.Architecture.Tests/InstallerGuards.cs can only hold the lines the behaviour rests on. This
    script installs it, upgrades it while a `bws` run holds bws.exe, installs a rebuild of the same
    version over it, refuses an older version, and uninstalls it with a file of somebody else's in its
    folder - and after every step asks the MACHINE, not the installer, what is there.

    One script for three places, so the three cannot drift apart: a clean Windows runner
    (.github/workflows/installer.yml), the throwaway virtual machine over ssh (Windows PowerShell 5.1,
    which is why this file is ASCII only and uses nothing from PowerShell 7), and a developer machine.

    THE KIT. Three installers built from the same two packages by packaging/build-msi.ps1:
        old   0.0.1  the packages as they are
        new   0.0.2  the packages with a few bytes added to both executables and to the notices - the
                     same file versions, other bytes
        same  0.0.2  the packages as they are, under the number of "new" - a release signed again under
                     its own number, whose files must replace the first build's
    The kit also carries kit.json: the hash of every file both ways, and the names the checks look for.
    So a machine that only RUNS a kit (the virtual machine) needs no packages, no WiX and no repository.

    WHAT IT ASKS after each step: the exit code, the entries in Programs and Features, every file in the
    install folder against the packages, the machine PATH, a NEW process finding `bws` by name and
    running it, the Start menu entry and the registry key the installer keeps. The window itself is not
    started - over ssh and on a runner there is no desktop to start it on.

    Every line is `ok`, `FAILED`, `note` or `NOT MEASURED` (with the reason). Nothing passes by default.

    Exit codes: 0 everything asked was ok, 1 something FAILED, 2 the starting state was not clean or the
    inputs are wrong (nothing was installed), 3 nothing FAILED but something was NOT MEASURED.

    SAFETY. It refuses to start when the product is already installed, so it never touches a copy
    somebody uses. It kills only the process it started itself, and on the way out - however it ends -
    uninstalls what it installed, by product code. Uninstalling by the .msi file would miss: every build
    has a new product code (measured in the owner's Bean Network Tester, 2026-09-09).

.PARAMETER Window
    The window package to build the kit from: the folder holding BetterWindowsServices.exe
    (dist/staging/BetterWindowsServices after packaging/build-dist.ps1). Not needed with -KitFolder.

.PARAMETER CommandLine
    The command line package: the folder holding bws.exe (dist/staging/bws).

.PARAMETER WorkFolder
    Where the kit (unless -KitFolder is given), the installer logs and the report go. Created if missing.

.PARAMETER KitFolder
    A kit built earlier with -BuildKitOnly, so a machine without WiX can run it.

.PARAMETER BuildKitOnly
    Build the kit into <WorkFolder>/kit and stop. Installs nothing.

.PARAMETER SelfTest
    The canary: install the old version, change one installed file, and require the folder check to SEE
    it - then uninstall. Proves the comparison every step rests on can fail.

.PARAMETER Cleanup
    Uninstall every copy of the product this machine has, by product code, and stop. Needs -KitFolder
    for the product's name.
#>
[CmdletBinding()]
param(
    [string] $Window = '',
    [string] $CommandLine = '',
    [string] $WorkFolder = '',
    [string] $KitFolder = '',
    [switch] $BuildKitOnly,
    [switch] $SelfTest,
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'

$script:Failed = 0
$script:Passed = 0
$script:NotMeasured = 0
$script:LogPath = ''
$script:Kit = $null
$script:Held = $null
$script:Touched = $false

# --- saying things ------------------------------------------------------------------------------

# Write-Host and never Write-Output: anything a function writes to the output becomes part of what it
# returns, and a caller then compares an array with 0 (measured in the owner's Testing Files Generator,
# 2026-09-28).
function Say([string] $line) {
    if ($script:LogPath) { Add-Content -LiteralPath $script:LogPath -Value $line -Encoding ASCII }
    Write-Host $line
}
function Check([bool] $ok, [string] $what) {
    if ($ok) { $script:Passed++; Say ('ok            ' + $what) }
    else { $script:Failed++; Say ('FAILED        ' + $what) }
}
function Note([string] $what) { Say ('note          ' + $what) }
function Unmeasured([string] $what) { $script:NotMeasured++; Say ('NOT MEASURED  ' + $what) }

function Stop-Inputs([string] $why) {
    Say ('STOP: ' + $why)
    exit 2
}

function Get-ExitCode {
    if ($script:Failed -gt 0) { return 1 }
    if ($script:NotMeasured -gt 0) { return 3 }
    return 0
}
function Write-Summary {
    Say ''
    Say ('passed: ' + $script:Passed + ', failed: ' + $script:Failed + ', not measured: ' + $script:NotMeasured)
}

# A step every later step stands on, and it did not happen. Going on would print a FAILED for every
# check after it, all of them saying the same thing, and then throw on a program that is not there -
# which is what the first run on a developer machine did (2026-10-14, Windows Installer busy with
# another package). The way out still runs the `finally` below, so nothing installed stays behind.
$script:Finishing = $false
function Stop-Early([string] $why, [bool] $measured) {
    if ($measured) { Check $false $why } else { Unmeasured $why }
    if ($script:Finishing) { return }
    Say 'the run stops here - every step after this one stands on it'
    Write-Summary
    exit (Get-ExitCode)
}
function Require([bool] $ok, [string] $what) {
    if ($ok) { Check $true $what } else { Stop-Early $what $true }
}

# What Windows Installer last wrote to the Application log - which package it is busy with, the
# place Microsoft's page on 1618 sends a reader to.
function Get-LastMsiEvent {
    $last = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'MsiInstaller' } -MaxEvents 1 -ErrorAction SilentlyContinue
    if (-not $last) { return '(none in the log)' }
    return ($last.TimeCreated.ToString('s') + ' ' + (($last.Message -split "`r?`n")[0]))
}

# --- the kit ------------------------------------------------------------------------------------

function Get-Sha([string] $path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }

# Every file of a flat package folder, name -> hash. Both packages are flat: an executable and two
# documents. A file both carry has to be the same bytes, or build-msi.ps1 refuses them first.
function Get-Hashes([string[]] $folders) {
    $set = [ordered]@{}
    foreach ($folder in $folders) {
        foreach ($file in @(Get-ChildItem -LiteralPath $folder -File -Force)) {
            if (-not $set.Contains($file.Name)) { $set[$file.Name] = Get-Sha $file.FullName }
        }
    }
    return $set
}

function Copy-Package([string] $from, [string] $into) {
    New-Item -ItemType Directory -Path $into -Force | Out-Null
    foreach ($file in @(Get-ChildItem -LiteralPath $from -File -Force)) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $into $file.Name)
    }
}

# A few bytes at the end of a file, so the file version stays and the bytes do not. Trailing bytes are
# harmless to a single-file .NET program, whose bundle is found through an offset written into the host
# - the kit asks the changed bws.exe to run before it is used, rather than trusting that sentence.
function Add-Bytes([string] $path) {
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Append)
    try {
        $bytes = [System.Text.Encoding]::ASCII.GetBytes('bws-installer-kit')
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

function New-Kit([string] $into) {
    $here = $PSScriptRoot
    $buildMsi = Join-Path $here 'build-msi.ps1'
    $registerPath = Join-Path $here 'components.json'
    foreach ($needed in @($buildMsi, $registerPath)) {
        if (-not (Test-Path -LiteralPath $needed)) { Stop-Inputs ("building a kit needs $needed - run this from the repository") }
    }
    foreach ($folder in @($Window, $CommandLine)) {
        if (-not $folder -or -not (Test-Path -LiteralPath $folder -PathType Container)) {
            Stop-Inputs '-Window and -CommandLine have to name the two package folders (dist/staging after packaging/build-dist.ps1)'
        }
    }
    $register = Get-Content -Raw -LiteralPath $registerPath | ConvertFrom-Json
    $source = Get-Content -Raw -LiteralPath $buildMsi
    if ($source -notmatch "\`$UpgradeCode = '([0-9A-F-]{36})'") { Stop-Inputs 'cannot read the UpgradeCode out of build-msi.ps1' }
    $upgradeCode = $Matches[1]

    if (Test-Path -LiteralPath $into) { Stop-Inputs "$into is already there - remove it or pass another -WorkFolder" }
    New-Item -ItemType Directory -Path $into | Out-Null
    $original = Join-Path $into 'payload-original'
    $modified = Join-Path $into 'payload-modified'
    Copy-Package $Window (Join-Path $original 'window')
    Copy-Package $CommandLine (Join-Path $original 'cli')
    Copy-Package $Window (Join-Path $modified 'window')
    Copy-Package $CommandLine (Join-Path $modified 'cli')
    $windowExe = [string]$register.packages.gui.executable
    $cliExe = [string]$register.packages.cli.executable
    Add-Bytes (Join-Path $modified ('window\' + $windowExe))
    Add-Bytes (Join-Path $modified ('cli\' + $cliExe))
    foreach ($side in @('window', 'cli')) { Add-Bytes (Join-Path $modified ($side + '\THIRD-PARTY-NOTICES.md')) }

    $said = & (Join-Path $modified ('cli\' + $cliExe)) --version 2>&1
    if ($LASTEXITCODE -ne 0) { Stop-Inputs ("the changed $cliExe does not run (exit $LASTEXITCODE): $said") }

    $builds = @(
        @{ Name = 'old.msi'; Tag = 'v0.0.1'; From = $original },
        @{ Name = 'new.msi'; Tag = 'v0.0.2'; From = $modified },
        @{ Name = 'same.msi'; Tag = 'v0.0.2'; From = $original })
    foreach ($build in $builds) {
        $out = Join-Path $into ('out-' + $build.Name)
        New-Item -ItemType Directory -Path $out | Out-Null
        & pwsh -NoProfile -File $buildMsi -Tag $build.Tag -Window (Join-Path $build.From 'window') `
            -CommandLine (Join-Path $build.From 'cli') -OutDir $out | Out-Host
        if ($LASTEXITCODE -ne 0) { Stop-Inputs ("build-msi.ps1 refused " + $build.Name + " (exit $LASTEXITCODE)") }
        Move-Item -LiteralPath (Join-Path $out ([string]$register.installer.file)) -Destination (Join-Path $into $build.Name)
        Remove-Item -LiteralPath $out -Recurse -Force
    }

    $kit = [ordered]@{
        productName = [string]$register.product.name
        publisher   = (([string]$register.product.supplier) -split ':\s*', 2)[-1]
        upgradeCode = $upgradeCode
        windowExe   = $windowExe
        cliExe      = $cliExe
        oldVersion  = '0.0.1'
        newVersion  = '0.0.2'
        original    = Get-Hashes @((Join-Path $original 'window'), (Join-Path $original 'cli'))
        modified    = Get-Hashes @((Join-Path $modified 'window'), (Join-Path $modified 'cli'))
    }
    [System.IO.File]::WriteAllText((Join-Path $into 'kit.json'), ($kit | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    Remove-Item -LiteralPath $original -Recurse -Force
    Remove-Item -LiteralPath $modified -Recurse -Force
    Write-Host ("kit written to $into")
}

function Read-Kit([string] $folder) {
    $path = Join-Path $folder 'kit.json'
    if (-not (Test-Path -LiteralPath $path)) { Stop-Inputs "$folder holds no kit.json - build one with -BuildKitOnly" }
    $kit = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    foreach ($name in @('old.msi', 'new.msi', 'same.msi')) {
        if (-not (Test-Path -LiteralPath (Join-Path $folder $name))) { Stop-Inputs "$folder holds no $name" }
    }
    return $kit
}

# A kit's hash set as a hashtable, from the object ConvertFrom-Json made of it.
function Get-Set([string] $which) {
    $set = @{}
    foreach ($property in $script:Kit.$which.PSObject.Properties) { $set[$property.Name] = [string]$property.Value }
    return $set
}

# --- what the machine says ----------------------------------------------------------------------

function Get-InstallFolder { return (Join-Path $env:ProgramFiles $script:Kit.productName) }
function Get-ShortcutPath { return (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) ($script:Kit.productName + '.lnk')) }
function Get-OwnKey { return ('HKLM:\Software\' + $script:Kit.publisher + '\' + $script:Kit.productName) }

# What Programs and Features lists under our name. @() because one entry comes back as a single object,
# which has no Count in Windows PowerShell 5.1 (measured in the owner's Testing Files Generator).
function Get-Entries {
    return @(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object { $_.PSObject.Properties['DisplayName'] -and $_.DisplayName -eq $script:Kit.productName })
}

function Get-MachinePath { return @(([Environment]::GetEnvironmentVariable('Path', 'Machine')) -split ';' | Where-Object { $_ }) }

# The machine PATH as the registry holds it: unexpanded, with its value kind. The entries above are
# EXPANDED, so a PATH written back with %SystemRoot% turned into C:\Windows would compare equal there and
# still be a changed machine. This is the copy a restore would need.
function Get-RawMachinePath {
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SYSTEM\CurrentControlSet\Control\Session Manager\Environment')
    try {
        return @{
            Kind = $key.GetValueKind('Path').ToString()
            Text = [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        }
    }
    finally {
        $key.Dispose()
    }
}

# THE KIND MAY CHANGE, AND ONLY WHEN NOTHING IN THE TEXT IS A VARIABLE - measured, not documented. On a
# clean runner (2026-10-14) Windows Installer wrote PATH back as String where it had been ExpandString,
# with the text the same to the character. On the virtual machine, whose PATH holds %SystemRoot%, the kind
# stayed. With no %variable% in the text the two kinds read the same for every reader, so the change is
# SAID and not failed - but a tool that later appends a %variable% without setting the kind would get it
# unexpanded (docs/08, 574). A kind that changed while the text holds a variable is a broken PATH.
function Test-RawPathKept($before, $after) {
    Check ($after.Text -ceq $before.Text) ('the registry text under it is the same to the character (' + $after.Text.Length + ' characters, ' + $before.Text.Length + ' before)')
    $variable = $before.Text.Contains('%')
    if ($after.Kind -eq $before.Kind) {
        Check $true ('and so is its kind (' + $after.Kind + ')')
    }
    elseif (-not $variable) {
        Note ('Windows Installer wrote PATH back as ' + $after.Kind + ', it was ' + $before.Kind + '. It holds no %variable%, so every reader expands it to the same entries')
    }
    else {
        Check $false ('its kind changed from ' + $before.Kind + ' to ' + $after.Kind + ' while it holds a %variable% - new processes read that entry unexpanded')
    }
}
function Get-OursOnPath {
    $folder = (Get-InstallFolder).TrimEnd('\')
    return @(Get-MachinePath | Where-Object { $_.TrimEnd('\') -eq $folder })
}

# The folder against a hash set: what is missing, what differs, what is extra. Empty means equal.
function Compare-Folder([string] $folder, $expected) {
    $found = @()
    if (-not (Test-Path -LiteralPath $folder)) { return @('the folder is not there') }
    $present = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $folder -Recurse -File -Force)) {
        $present[$file.FullName.Substring($folder.Length + 1)] = Get-Sha $file.FullName
    }
    foreach ($name in $expected.Keys) {
        if (-not $present.ContainsKey($name)) { $found += "missing $name" }
        elseif ($present[$name] -ne $expected[$name]) { $found += "$name differs" }
    }
    foreach ($name in $present.Keys) {
        if (-not $expected.ContainsKey($name)) { $found += "extra $name" }
    }
    return $found
}

# Asked from a NEW process, with the PATH a new terminal would read - a PATH entry only reaches processes
# started after it was written, and this script's own environment is older than the install.
function Get-BwsAnswer {
    $saved = $env:Path
    try {
        $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
        $where = @(& cmd.exe /c ('where ' + [System.IO.Path]::GetFileNameWithoutExtension($script:Kit.cliExe)) 2>$null)
        $version = @(& cmd.exe /c ([System.IO.Path]::GetFileNameWithoutExtension($script:Kit.cliExe) + ' --version') 2>&1)
        $code = $LASTEXITCODE
        return @{ Where = $where; Said = $version; Code = $code }
    }
    finally {
        $env:Path = $saved
    }
}

function Test-Installed([string] $version, [string] $which) {
    $entries = @(Get-Entries)
    Check ($entries.Count -eq 1) ('one entry in Programs and Features (' + (($entries | ForEach-Object { $_.DisplayVersion + ' ' + $_.PSChildName }) -join ', ') + ')')
    if ($entries.Count -ge 1) {
        $entry = $entries[0]
        Check ($entry.DisplayVersion -eq $version) ('it says ' + $version + ' (' + $entry.DisplayVersion + ')')
        Check ($entry.Publisher -eq $script:Kit.publisher) ('the publisher is ' + $script:Kit.publisher + ' (' + $entry.Publisher + ')')
        $noModify = $entry.PSObject.Properties['NoModify'] -and $entry.NoModify -eq 1
        $noRepair = $entry.PSObject.Properties['NoRepair'] -and $entry.NoRepair -eq 1
        Check ($noModify -and $noRepair) 'it offers neither Change nor Repair'
        $about = ''
        if ($entry.PSObject.Properties['URLInfoAbout']) { $about = $entry.URLInfoAbout }
        $icon = ''
        if ($entry.PSObject.Properties['DisplayIcon']) { $icon = $entry.DisplayIcon }
        Note ('about ' + $about + ', icon ' + $icon)
    }
    $folder = Get-InstallFolder
    $differences = @(Compare-Folder $folder (Get-Set $which))
    Check ($differences.Count -eq 0) ('the folder holds exactly the ' + $which + ' packages (' + ($differences -join ', ') + ')')
    Check (@(Get-OursOnPath).Count -eq 1) ('the folder is on the machine PATH once (' + @(Get-OursOnPath).Count + ')')
    $answer = Get-BwsAnswer
    $ours = Join-Path $folder $script:Kit.cliExe
    Check (@($answer.Where | Where-Object { $_ -eq $ours }).Count -eq 1) ('a new process finds ' + $ours + ' by name (' + ($answer.Where -join ', ') + ')')
    if (@($answer.Where).Count -gt 0 -and $answer.Where[0] -ne $ours) { Note ('another one comes first on PATH: ' + $answer.Where[0]) }
    Check ($answer.Code -eq 0 -and ([string]($answer.Said | Select-Object -First 1)).StartsWith('bws ')) ('and it runs (exit ' + $answer.Code + ': ' + ($answer.Said | Select-Object -First 1) + ')')
    $shortcut = Get-ShortcutPath
    Check (Test-Path -LiteralPath $shortcut) ('the Start menu of every account has ' + $shortcut)
    if (Test-Path -LiteralPath $shortcut) {
        $link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
        Check ($link.TargetPath -eq (Join-Path $folder $script:Kit.windowExe)) ('it starts the window (' + $link.TargetPath + ')')
        Check ($link.WorkingDirectory.TrimEnd('\') -eq $folder) ('it starts in the install folder (' + $link.WorkingDirectory + ')')
    }
    Check (Test-Path -LiteralPath (Get-OwnKey)) ('the installer keeps ' + (Get-OwnKey))
}

# --- doing things -------------------------------------------------------------------------------

# msiexec with the whole command line as one string and a limit on the wait. Measured in the owner's
# Testing Files Generator: a nested quote swallowed the file name, and msiexec waited for a help window in
# session 0 that nobody could close.
#
# 1618 IS NOT AN ANSWER ABOUT THIS INSTALLER. It is ERROR_INSTALL_ALREADY_RUNNING, which Windows Installer
# returns while another package holds its _MSIExecute mutex (learn.microsoft.com, "_MSIExecute Mutex",
# read 2026-10-14). So it is waited out - thirty seconds at a time, up to five minutes - and a machine still
# busy after that makes the step NOT MEASURED rather than FAILED. Measured the same day on a developer
# machine: the first install answered 1618 in 3 s while something else was installing.
$script:LastMsi = ''
function Invoke-Msi([string] $verb, [string] $target, [string] $logName) {
    $log = Join-Path $WorkFolder $logName
    $arguments = $verb + ' "' + $target + '" /qn /norestart /l*v "' + $log + '"'
    $started = Get-Date
    $code = -1
    for ($attempt = 1; $attempt -le 11; $attempt++) {
        $process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList $arguments -PassThru
        # Read once so the exit code is kept after the process ends (Windows PowerShell 5.1).
        $null = $process.Handle
        $script:Touched = $true
        if (-not $process.WaitForExit(600000)) {
            $script:LastMsi = '> msiexec ' + $arguments + '  -> did not finish in 600 s'
            return -1
        }
        $code = $process.ExitCode
        if ($code -ne 1618 -or $attempt -eq 11) { break }
        if ($attempt -eq 1) { Note ('another installation is in progress (1618) - waiting, 30 s at a time, up to 5 minutes. Windows Installer last logged: ' + (Get-LastMsiEvent)) }
        Start-Sleep -Seconds 30
    }
    $script:LastMsi = '> msiexec ' + $arguments + '  -> exit ' + $code + ' in ' + [int]((Get-Date) - $started).TotalSeconds + ' s'
    if ($code -eq 1618) {
        Say $script:LastMsi
        Stop-Early ('another installation stayed in progress for 5 minutes (1618), so this step could not be asked. Windows Installer last logged: ' + (Get-LastMsiEvent)) $false
    }
    return $code
}

# A bws run kept in progress without writing anything: its output goes into a pipe nobody reads, and the
# listing is far longer than the pipe holds (about 1.2 KB per entry, measured 2026-10-14 - 976 KB for 800).
function Start-HeldRun([string] $exe) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $exe
    $info.Arguments = 'list --json'
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.CreateNoWindow = $true
    $process = [System.Diagnostics.Process]::Start($info)
    Start-Sleep -Seconds 3
    return $process
}

function Remove-AllCopies {
    foreach ($entry in @(Get-Entries)) {
        if ($entry.PSChildName -notmatch '^\{[0-9A-Fa-f-]{36}\}$') {
            Note ('an entry named ' + $script:Kit.productName + ' that is not a Windows Installer product: ' + $entry.PSChildName + ' - left alone')
            continue
        }
        $code = Invoke-Msi '/x' $entry.PSChildName ('cleanup-' + $entry.PSChildName + '.log')
        Say $script:LastMsi
        Check ($code -eq 0 -or $code -eq 3010) ('removed ' + $entry.PSChildName + ' (' + $code + ')')
    }
}

function Test-CleanStart {
    $folder = Get-InstallFolder
    $why = @()
    if (@(Get-Entries).Count -ne 0) { $why += ('entries in Programs and Features: ' + @(Get-Entries).Count) }
    if (Test-Path -LiteralPath $folder) { $why += ('the folder ' + $folder + ' is there') }
    if (@(Get-OursOnPath).Count -ne 0) { $why += 'the folder is on the machine PATH' }
    if (Test-Path -LiteralPath (Get-ShortcutPath)) { $why += ('the shortcut ' + (Get-ShortcutPath) + ' is there') }
    return $why
}

# --- the run ------------------------------------------------------------------------------------

if (-not $WorkFolder) { $WorkFolder = Join-Path ([System.IO.Path]::GetTempPath()) 'bws-installer-test' }
New-Item -ItemType Directory -Path $WorkFolder -Force | Out-Null
$WorkFolder = (Resolve-Path -LiteralPath $WorkFolder).Path

if ($BuildKitOnly) {
    New-Kit (Join-Path $WorkFolder 'kit')
    exit 0
}

if (-not $KitFolder) {
    $KitFolder = Join-Path $WorkFolder 'kit'
    New-Kit $KitFolder
}
$KitFolder = (Resolve-Path -LiteralPath $KitFolder).Path
$script:Kit = Read-Kit $KitFolder

$script:LogPath = Join-Path $WorkFolder ('installer-' + $env:COMPUTERNAME + '.txt')
if (Test-Path -LiteralPath $script:LogPath) { Remove-Item -LiteralPath $script:LogPath -Force }

$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Say ('machine ' + $env:COMPUTERNAME + ', ' + (Get-Date -Format s) + ', PowerShell ' + $PSVersionTable.PSVersion + ', elevated ' + $elevated)
if (-not $elevated) { Stop-Inputs 'installing for the machine needs an elevated session' }

try {
    if ($Cleanup) {
        Remove-AllCopies
        Say ('failed: ' + $script:Failed)
        if ($script:Failed -gt 0) { exit 1 }
        exit 0
    }

    # The state this starts from is asserted, not assumed - and a copy somebody uses is never touched.
    $dirty = @(Test-CleanStart)
    if ($dirty.Count -gt 0) { Stop-Inputs ('something of ours is here already: ' + ($dirty -join ', ') + '. Remove it, or run -Cleanup -KitFolder <kit> when it is a test copy') }
    $pathBefore = @(Get-MachinePath)
    $rawBefore = Get-RawMachinePath
    Note ('the machine PATH before: ' + $rawBefore.Kind + ', ' + $rawBefore.Text.Length + ' characters, holds a %variable%: ' + $rawBefore.Text.Contains('%'))

    $old = Join-Path $KitFolder 'old.msi'
    $new = Join-Path $KitFolder 'new.msi'
    $same = Join-Path $KitFolder 'same.msi'
    $folder = Get-InstallFolder

    Say ''
    Say ('== install ' + $script:Kit.oldVersion)
    $code = Invoke-Msi '/i' $old 'install-old.log'
    Say $script:LastMsi
    Require ($code -eq 0) ('installed (' + $code + ')')
    Test-Installed $script:Kit.oldVersion 'original'

    if ($SelfTest) {
        Say ''
        Say '== the canary: one installed file changed, and the folder check has to see it'
        Add-Bytes (Join-Path $folder 'LICENSE')
        $differences = @(Compare-Folder $folder (Get-Set 'original'))
        Check ($differences.Count -eq 1 -and $differences[0] -eq 'LICENSE differs') ('the folder check sees it (' + ($differences -join ', ') + ')')
        $notClean = @(Test-CleanStart)
        Check ($notClean.Count -ge 3) ('the starting-state check sees an install (' + ($notClean -join ', ') + ')')
    }
    else {
        Say ''
        Say ('== upgrade to ' + $script:Kit.newVersion + ' while a bws run holds bws.exe')
        $script:Held = Start-HeldRun (Join-Path $folder $script:Kit.cliExe)
        $holding = -not $script:Held.HasExited
        if (-not $holding) { Unmeasured 'the held bws run ended before the upgrade, so nothing held the file' }
        $code = Invoke-Msi '/i' $new 'upgrade-held.log'
        Say $script:LastMsi
        if ($holding) {
            # 0, MEASURED, and not the 3010 predicted from the owner's other projects. The old product goes
            # first here (afterInstallInitialize), so the file in use is REMOVED - moved into Config.Msi -
            # rather than replaced, the new bws.exe lands where nothing is, and only that moved copy waits
            # for the next restart, which Windows Installer does not count as a restart the install needs
            # (the log of 2026-10-14 on the virtual machine: "1 application(s) had been reported to have
            # files in use", then "Scheduling reboot operation: Deleting file C:\Config.Msi\...rbf", then
            # success). 3010 is a success too, so it is accepted and said rather than refused.
            Require ($code -eq 0 -or $code -eq 3010) ('the upgrade succeeds with bws.exe in use (' + $code + ')')
            if ($code -eq 3010) { Note '3010 - Windows asks for a restart to finish' }
            # WHAT BECOMES OF THE RUN IS SAID, NOT HELD TO ONE ANSWER - because the answer is not the
            # installer's. bws is a single-file .NET program and reads parts of itself out of bws.exe the
            # first time it needs them, and the upgrade has moved that file away. On the virtual machine
            # the run needed nothing more and finished with 0. On a clean runner (2026-10-14) it next
            # opened its error channel, needed System.Text.Encoding.Extensions, did not find the file and
            # ended with an unhandled exception. So a copy left running may stop with an error, and the
            # README tells a person to close it first - this line says, every run, which of the two it was.
            $runningAfter = -not $script:Held.HasExited
            [void]$script:Held.StandardOutput.ReadToEnd()
            [void]$script:Held.WaitForExit(60000)
            if ($script:Held.HasExited -and $script:Held.ExitCode -eq 0) {
                Note ('the bws run outlived the upgrade and finished with 0 (still running right after it: ' + $runningAfter + ')')
            }
            else {
                Note ('the bws run did NOT outlive the upgrade (exit ' + $script:Held.ExitCode + ') - it read part of itself from the bws.exe the upgrade moved away. The README says to close it first')
            }
        }
        else {
            Require ($code -eq 0 -or $code -eq 3010) ('upgraded (' + $code + ')')
        }
        $pending = @()
        $session = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -ErrorAction SilentlyContinue
        if ($session -and $session.PSObject.Properties['PendingFileRenameOperations']) {
            $pending = @($session.PendingFileRenameOperations | Where-Object { $_ -like '*Config.Msi*' })
        }
        Note ('left for the next restart: ' + $pending.Count + ' Config.Msi entr(y/ies)')
        Test-Installed $script:Kit.newVersion 'modified'
        $firstCode = ''
        if (@(Get-Entries).Count -ge 1) { $firstCode = @(Get-Entries)[0].PSChildName }

        Say ''
        Say ('== a rebuild of ' + $script:Kit.newVersion + ', the same version with another product code and the original bytes')
        $code = Invoke-Msi '/i' $same 'same-version.log'
        Say $script:LastMsi
        Require ($code -eq 0) ('installed (' + $code + ')')
        Test-Installed $script:Kit.newVersion 'original'
        $secondCode = ''
        if (@(Get-Entries).Count -ge 1) { $secondCode = @(Get-Entries)[0].PSChildName }
        Check ($firstCode -and $secondCode -and $firstCode -ne $secondCode) ('the rebuild replaced the first build (' + $firstCode + ' -> ' + $secondCode + ')')

        Say ''
        Say ('== ' + $script:Kit.oldVersion + ' over ' + $script:Kit.newVersion)
        $code = Invoke-Msi '/i' $old 'downgrade.log'
        Say $script:LastMsi
        Check ($code -ne 0 -and $code -ne 3010) ('refused (' + $code + ')')
        $said = Get-Content -LiteralPath (Join-Path $WorkFolder 'downgrade.log') -Raw -ErrorAction SilentlyContinue
        $sentence = 'A newer version of ' + $script:Kit.productName + ' is already installed.'
        Check ($said -and $said.Contains($sentence)) ('with the sentence the package carries: ' + $sentence)
        Test-Installed $script:Kit.newVersion 'original'
    }

    Say ''
    Say '== uninstall, with a file of somebody else in the folder'
    $planted = Join-Path $folder 'somebody-elses.txt'
    Set-Content -LiteralPath $planted -Value 'not ours' -Encoding ASCII
    $productCode = ''
    if (@(Get-Entries).Count -ge 1) { $productCode = @(Get-Entries)[0].PSChildName }
    $code = Invoke-Msi '/x' $productCode 'uninstall.log'
    Say $script:LastMsi
    Check ($code -eq 0) ('uninstalled (' + $code + ')')
    Check (@(Get-Entries).Count -eq 0) 'no entry in Programs and Features'
    $left = @()
    if (Test-Path -LiteralPath $folder) { $left = @(Get-ChildItem -LiteralPath $folder -Recurse -Force | ForEach-Object { $_.Name }) }
    Check ($left.Count -eq 1 -and $left[0] -eq 'somebody-elses.txt') ('the folder holds only the planted file (' + ($left -join ', ') + ')')
    $pathAfter = @(Get-MachinePath)
    Check (($pathAfter -join ';') -eq ($pathBefore -join ';')) ('the machine PATH is what it was before the install (' + $pathAfter.Count + ' entries, ' + $pathBefore.Count + ' before)')
    Test-RawPathKept $rawBefore (Get-RawMachinePath)
    Check (-not (Test-Path -LiteralPath (Get-ShortcutPath))) 'no Start menu entry'
    Check (-not (Test-Path -LiteralPath (Get-OwnKey))) ('no ' + (Get-OwnKey))
    Note ('HKLM:\Software\' + $script:Kit.publisher + ' is there: ' + (Test-Path -LiteralPath ('HKLM:\Software\' + $script:Kit.publisher)))
    Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    Check (-not (Test-Path -LiteralPath $folder)) 'the planted file and its folder removed by this script'
}
finally {
    $script:Finishing = $true
    if ($script:Held -and -not $script:Held.HasExited) {
        Say ('ending the bws run this script started (' + $script:Held.Id + ')')
        $script:Held.Kill()
    }
    # However the run ended, nothing it installed stays behind. Only after something was installed, so a
    # run that stopped at a dirty start never removes a copy somebody uses.
    if ($script:Touched -and -not $Cleanup -and @(Get-Entries).Count -gt 0) {
        Say 'the run ended with the product installed - removing it'
        Remove-AllCopies
    }
}

Write-Summary
exit (Get-ExitCode)
