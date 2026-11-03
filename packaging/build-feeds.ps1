#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Render the WinGet manifests and the Chocolatey packages of one published release.

.DESCRIPTION
    Four packages, two per feed (the owner's decision of 2026-11-03):

                    the window                              the command line
      WinGet        DonislawDev.BetterWindowsServices       DonislawDev.BetterWindowsServices.CLI
      Chocolatey    better-windows-services                 better-windows-services-cli
      installs      the Windows installer, and in WinGet    bws-cli-win-x64.zip
                    the window zip second, for --scope user

    The window's packages install the Windows installer because only an installer puts a program in the
    Start menu: WinGet makes no shortcut for a portable package and its manifest has no field for one. The
    installer carries the command line too, so the window's packages put bws on the machine PATH. The order
    and the reasons stand in the templates, beside the lines they are about. The same script, renderer and
    guard shape as the owner's ChronoMock (its packaging/build-feeds.ps1, 2026-11-02), which was measured on
    both feeds before this one was written.

    Every value comes from one place. The release folder is the one a person downloads from the release
    page. Its SHA256SUMS is the one file taken as it is: every other file is hashed here and has to match
    its line in it, and what ties those files to this project is their signature, not the sums. The
    installer, and the program in each archive, has to be signed by the certificate packaging/codesign.json
    pins, with a timestamp, the way verify-release.yml checks a published release. The installer has to
    carry the tag's version and the UpgradeCode build-msi.ps1 builds with, and each archive has to hold its
    program at the version of the tag. Our file names carry no version, so these checks are what tell a
    folder of another release apart.
    The archive names, the program in each and the installer's file name come from packaging/components.json
    - the register sign-release.ps1 signs from and verify-release.yml checks from, so the three cannot name
    two different programs. The installer's name, UpgradeCode and shortcut sentence come from build-msi.ps1
    (its -SourceOnly output, the source the installer was built from), the release date from CHANGELOG.md,
    the templates from packaging/winget and packaging/chocolatey.

    Written to -OutDir, which must not exist yet, under a parent that does, outside the repository:
      winget/manifests/d/DonislawDev/BetterWindowsServices/<version>/       the three files of one WinGet pull request
      winget/manifests/d/DonislawDev/BetterWindowsServices/CLI/<version>/   and the three of the other
      chocolatey/better-windows-services/ and chocolatey/better-windows-services-cli/   one folder per `choco pack`
    Rendered into a working folder beside it and renamed at the end, so a refused run leaves nothing, and a
    second run given the same -OutDir is refused rather than mixed into the first.

    Nothing here submits anything. A pull request to microsoft/winget-pkgs and a `choco push` publish under
    the project's name to feeds somebody else moderates, so they stay a step a person says yes to.

    Exit codes: 0 rendered, 1 refused - one line on stderr naming the input and why.

.PARAMETER Tag
    The published release, e.g. v0.5.0. A release candidate has no installer, so it gets no packages.

.PARAMETER Release
    The folder holding that release's SHA256SUMS, BetterWindowsServices-win-x64.msi,
    BetterWindowsServices-win-x64.zip and bws-cli-win-x64.zip, as downloaded from the release page.

.PARAMETER OutDir
    Where the packages go. Must not exist, and must be outside the repository.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string] $Release,
    [Parameter(Mandatory)] [string] $OutDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$wingetTemplates = Join-Path $PSScriptRoot 'winget'
$chocoTemplates = Join-Path $PSScriptRoot 'chocolatey'

# The manifest schema. Take it from manifests merged into microsoft/winget-pkgs, not from that repository's
# schema folder: a version can be documented there before the validation service accepts it (refused within
# minutes on 2026-08-25 in a sibling project). On 2026-10-30 the repository's bot called 1.12.0 deprecated
# on a pending pull request and asked for 1.28.0, and 1.28.0 defines every field used here.
$WingetSchema = '1.28.0'

# The most bytes one program may take out of its archive. The biggest is the window, 172,065,200 bytes in
# v0.5.0 (read 2026-11-03), so this leaves it room to grow for years and still stops an archive that would
# fill the disk before its signature is asked.
$ProgramCap = 1GB

# A refusal is one plain line on stderr and exit 1, the way build-msi.ps1 refuses. `exit` still runs the
# `finally` that removes the working folders.
function Stop-Render([string] $message) {
    [Console]::Error.WriteLine("build-feeds: $message")
    exit 1
}

# What went wrong in a .NET or COM call, without the line PowerShell wraps it in (in the language of the
# machine, which is not always English).
function Get-Reason([System.Management.Automation.ErrorRecord] $record) {
    $exception = $record.Exception
    if ($exception.InnerException) { $exception = $exception.InnerException }
    return $exception.Message
}

# --- what the release has to be ------------------------------------------------------------------------

function Get-ReleaseVersion([string] $releaseTag) {
    if ($releaseTag.Contains('-')) {
        Stop-Render "$releaseTag is a release candidate. Candidates get no installer, so they get no packages"
    }
    if ($releaseTag -notmatch '^v(\d+)\.(\d+)\.(\d+)$') {
        Stop-Render "'$releaseTag' is not a release tag. Pass it the way the release is tagged, for example v0.5.0"
    }
    return "$([int]$Matches[1]).$([int]$Matches[2]).$([int]$Matches[3])"
}

# SHA256SUMS of the release as name -> lower-case hash. A release's file is under a kilobyte, so anything over
# a megabyte is not one, and a name listed twice is a file that cannot be trusted either way.
function Read-Sums([string] $path) {
    $item = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
    if (-not $item -or $item.PSIsContainer) { Stop-Render "$path is not there. Download SHA256SUMS of the release into -Release" }
    if ($item.Length -gt 1MB) { Stop-Render "$path holds $($item.Length) bytes, and a release's SHA256SUMS is under a kilobyte" }
    try {
        $text = [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false, $true))
    }
    catch {
        Stop-Render "$path is not UTF-8 text"
    }
    $sums = @{}
    foreach ($line in ($text -split "`r?`n")) {
        if (-not $line) { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64}) [ *]?(\S+)$') { Stop-Render "$path has a line that is not '<sha256>  <file>': $line" }
        $name = $Matches[2]
        if ($sums.ContainsKey($name)) { Stop-Render "$path lists $name twice" }
        $sums[$name] = $Matches[1].ToLowerInvariant()
    }
    return $sums
}

# The file's hash, computed here, and the one SHA256SUMS gives for it, which must agree.
function Assert-Hashed([hashtable] $sums, [string] $folder, [string] $name) {
    if (-not $sums.ContainsKey($name)) { Stop-Render "SHA256SUMS in $folder has no line for $name" }
    $path = Join-Path $folder $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Stop-Render "$name is not in $folder. Download it from the release page" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $sums[$name]) { Stop-Render "$name in $folder hashes to $hash, and SHA256SUMS says $($sums[$name])" }
    return $hash
}

# Property rows of an installer database. Every COM object is released on every path: a held database
# keeps the file open.
function Read-MsiProperties([string] $msi) {
    $installer = $null
    $database = $null
    $view = $null
    $properties = @{}
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($msi, 0)
        $view = $database.OpenView('SELECT `Property`, `Value` FROM `Property`')
        [void]$view.Execute()
        while ($true) {
            $record = $view.Fetch()
            if ($null -eq $record) { break }
            try { $properties[$record.StringData(1)] = $record.StringData(2) }
            finally { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($record) }
        }
        [void]$view.Close()
    }
    catch {
        Stop-Render "$msi could not be read as an installer database: $(Get-Reason $_)"
    }
    finally {
        foreach ($held in @($view, $database, $installer)) {
            if ($null -ne $held) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($held) }
        }
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
    }
    return $properties
}

# The program in each archive, as its name in the archive - the same "<folder>/<executable>" sign-release.ps1
# builds its $OURS from, out of the same register, so what is signed and what is packaged are one list.
function Get-PackagedPrograms($register) {
    $programs = [ordered]@{}
    foreach ($entry in $register.packages.PSObject.Properties) {
        $zip = [string]$entry.Value.zip
        $folder = [string]$entry.Value.folder
        $executable = [string]$entry.Value.executable
        if (-not $zip -or -not $folder -or -not $executable) {
            Stop-Render "packaging/components.json names the package '$($entry.Name)' without its zip, folder and executable"
        }
        $programs[$entry.Name] = [ordered]@{ Zip = $zip; Program = "$folder/$executable" }
    }
    foreach ($needed in @('gui', 'cli')) {
        if (-not $programs.Contains($needed)) { Stop-Render "packaging/components.json has no '$needed' package, and the feeds package both halves" }
    }
    return $programs
}

# The program a package starts has to be in its archive at that path and at the tag's version. It is copied
# out into the scratch folder so that its signature can be asked, and the copy is returned. An archive of
# another release is refused here and not by the person who installs it.
function Expand-Checked([string] $zip, [string] $program, [string] $version, [string] $scratch) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipName = Split-Path -Leaf $zip
    $archive = $null
    try {
        try {
            $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        }
        catch {
            Stop-Render "$zipName could not be read as an archive: $(Get-Reason $_)"
        }
        $entry = $archive.GetEntry($program)
        if ($null -eq $entry) { Stop-Render "$zipName holds no $program, which its package starts and the release signs" }
        if ($entry.Length -gt $ProgramCap) { Stop-Render "$zipName declares $program at $($entry.Length) bytes, and no program of ours comes near $ProgramCap" }
        $copy = Join-Path $scratch ([guid]::NewGuid().ToString('N') + [System.IO.Path]::GetExtension($program))
        # Copied by hand and counted, not by ExtractToFile: the .NET reader stops a compressed entry at the
        # size its header declares, but reads a stored one to the end of its data whatever the header says
        # (tools/feeds/zip-bound-probe.ps1 on .NET 10.0.14, 2026-11-03). So the count is what bounds the
        # bytes written, and an archive whose entry holds more than it declares is refused as it is.
        $from = $null
        $into = $null
        try {
            $from = $entry.Open()
            $into = [System.IO.File]::Open($copy, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            $buffer = [byte[]]::new(1MB)
            $written = [long]0
            while (($read = $from.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $written += $read
                if ($written -gt $entry.Length) { Stop-Render "$zipName holds more of $program than the $($entry.Length) bytes it declares" }
                $into.Write($buffer, 0, $read)
            }
        }
        catch {
            Stop-Render "$program could not be read out of $zipName`: $(Get-Reason $_)"
        }
        finally {
            if ($null -ne $into) { $into.Dispose() }
            if ($null -ne $from) { $from.Dispose() }
        }
    }
    finally {
        if ($null -ne $archive) { $archive.Dispose() }
    }
    # The product version of our programs carries the commit after a plus, 0.5.0+c9827633... for v0.5.0
    # (read 2026-11-03 from both published executables), so the version is the part before it. The commit
    # is not asked: that needs the tag in the clone, and the guard runs on clones that have none.
    $found = [string](Get-Item -LiteralPath $copy).VersionInfo.ProductVersion
    if (($found -split '\+', 2)[0] -ne $version) { Stop-Render "$program in $zipName is version '$found', and the tag is $version" }
    return $copy
}

# Why a file is not signed the way a published release is, or nothing when it is. The questions
# verify-release.yml asks: Valid says only that somebody's certificate signed it, so the certificate has to
# be the one packaging/codesign.json pins, and without a timestamp a signature dies with the certificate.
function Get-SignatureFault([string] $path, [string] $pin) {
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid') { return "signature status $($signature.Status)" }
    $signer = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($signature.SignerCertificate.RawData)).ToLowerInvariant()
    if ($signer -ne $pin) { return "signed by a certificate with SHA-256 $signer" }
    if (-not $signature.TimeStamperCertificate) { return 'signed with no timestamp' }
    return ''
}

# What build-msi.ps1 builds the installer from, asked of the script itself rather than copied out of it.
function Get-InstallerIdentity([string] $releaseTag) {
    # A process of its own: its refusals end the process they run in.
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $source = (& $pwsh -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'build-msi.ps1') -Tag $releaseTag -SourceOnly) -join "`n"
    if ($LASTEXITCODE -ne 0) { Stop-Render "build-msi.ps1 -SourceOnly refused $releaseTag (its line above says why)" }
    $identity = @{}
    $patterns = [ordered]@{
        Name        = '<Package\b[^>]*?\sName="([^"]+)"'
        UpgradeCode = '<Package\b[^>]*?\sUpgradeCode="([0-9A-F-]{36})"'
        Shortcut    = '<Shortcut\b[^>]*?\sDescription="([^"]+)"'
    }
    foreach ($key in $patterns.Keys) {
        $found = [regex]::Matches($source, $patterns[$key])
        if ($found.Count -ne 1) { Stop-Render "build-msi.ps1 -SourceOnly gave $($found.Count) values for the installer's $key, and one is needed" }
        $identity[$key] = $found[0].Groups[1].Value
    }
    return $identity
}

function Get-ReleaseDate([string] $version) {
    $changelog = Join-Path $root 'CHANGELOG.md'
    $dates = [regex]::Matches([System.IO.File]::ReadAllText($changelog), "(?m)^## \[$([regex]::Escape($version))\] - (\d{4}-\d{2}-\d{2})\s*$")
    if ($dates.Count -ne 1) { Stop-Render "CHANGELOG.md dates $version $($dates.Count) times, and a release is dated once" }
    return $dates[0].Groups[1].Value
}

# --- where the packages go -----------------------------------------------------------------------------

# A path with every link and junction on the way to it followed, so a link does not get round the refusal
# to write into the repository. Segments that do not exist yet are kept as written.
function Resolve-RealPath([string] $path) {
    $full = [System.IO.Path]::GetFullPath($path)
    $current = [System.IO.Path]::GetPathRoot($full)
    foreach ($segment in $full.Substring($current.Length).Split([System.IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries)) {
        $next = Join-Path $current $segment
        $item = Get-Item -LiteralPath $next -Force -ErrorAction SilentlyContinue
        if ($item -and $item.LinkType) { $next = $item.ResolveLinkTarget($true).FullName }
        $current = $next
    }
    return $current.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

function Assert-Destination([string] $path) {
    $real = Resolve-RealPath $path
    $repo = Resolve-RealPath $root
    if ($real -eq $repo -or $real.StartsWith($repo + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Render "$path is inside the repository. Render outside it, so nothing rendered can be committed by mistake"
    }
    if (Test-Path -LiteralPath $path) { Stop-Render "$path exists already. Give a folder that does not, so nothing old is mixed in" }
    $parent = Split-Path -Parent ([System.IO.Path]::GetFullPath($path))
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { Stop-Render "$parent, the folder $path would go in, does not exist" }
}

# --- rendering -----------------------------------------------------------------------------------------

# A value goes into YAML, XML and PowerShell alike, so it holds nothing any of them reads as syntax: no quote
# of either kind, no backtick or dollar for PowerShell, no angle bracket or ampersand for XML, no doubled
# brace that would read as a placeholder. Prose is written without apostrophes for that reason.
$unsafe = [regex]'[''"`$<>&]|\{\{|\}\}|\r'

# Fills every placeholder. A value of several lines placed on a line of its own gets that line's indentation
# on each of its lines, which is what a YAML block needs, and blank lines stay blank. An unknown placeholder
# is refused before anything is replaced, because a refusal from inside the replacement would be raised in a
# .NET callback rather than end the run.
function Expand-Template([string] $templatePath, [System.Collections.IDictionary] $values, [System.Collections.Generic.HashSet[string]] $used) {
    $text = [System.IO.File]::ReadAllText($templatePath).Replace("`r`n", "`n")
    $placeholder = '(?m)(^[ ]*)?\{\{([A-Z0-9_]+)\}\}'
    foreach ($asked in [regex]::Matches($text, $placeholder)) {
        $name = $asked.Groups[2].Value
        if (-not $values.Contains($name)) { Stop-Render "$templatePath asks for {{$name}}, which build-feeds.ps1 does not give" }
        [void]$used.Add($name)
    }
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)
        $indent = $m.Groups[1].Value
        $lines = ([string]$values[$m.Groups[2].Value]).Split("`n")
        return (($lines | ForEach-Object { if ($_) { $indent + $_ } else { '' } }) -join "`n")
    }.GetNewClosure()
    $out = [regex]::Replace($text, $placeholder, $evaluator)
    if ($out.Contains('{{') -or $out.Contains('}}')) { Stop-Render "$templatePath still holds a doubled brace after rendering" }
    if ($out -match '[^\x09\x0A\x20-\x7E]') {
        # Windows PowerShell 5.1 reads a script without a byte order mark as ANSI, and every file here is
        # read by somebody else's parser, so ASCII is the one encoding nothing can get wrong.
        Stop-Render "$templatePath renders to text that is not ASCII"
    }
    return $out
}

function Write-Rendered([string] $path, [string] $text) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
    [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
}

# --- the run -------------------------------------------------------------------------------------------

$version = Get-ReleaseVersion $Tag
# Against the location of this session, as PowerShell resolves a path. .NET resolves a relative path against
# the process folder, which Set-Location does not move, so `out` given after a cd would be another folder.
$OutDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
$Release = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Release)
Assert-Destination $OutDir
$releaseDir = [System.IO.Path]::GetFullPath($Release)
if (-not (Test-Path -LiteralPath $releaseDir -PathType Container)) { Stop-Render "$Release is not a folder. Download the release files into one" }

$register = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'components.json') | ConvertFrom-Json
$product = $register.product
$publisher = ($product.supplier -split ':\s*', 2)[-1]
foreach ($field in @($product.name, $publisher, $product.homepage, $product.source, $product.license, $product.copyright)) {
    if (-not $field) { Stop-Render 'packaging/components.json is missing a product field the packages name' }
}
$msiName = [string]$register.installer.file
if (-not $msiName) { Stop-Render 'packaging/components.json names no installer file, and the window packages install it' }
$packaged = Get-PackagedPrograms $register
$appZipName = $packaged.gui.Zip
$cliZipName = $packaged.cli.Zip
# WinGet takes the path inside the archive the Windows way.
$appZipExe = $packaged.gui.Program.Replace('/', '\')
$cliZipExe = $packaged.cli.Program.Replace('/', '\')

$identity = Get-InstallerIdentity $Tag
$releaseDate = Get-ReleaseDate $version
$sums = Read-Sums (Join-Path $releaseDir 'SHA256SUMS')
$msiHash = Assert-Hashed $sums $releaseDir $msiName
$appZipHash = Assert-Hashed $sums $releaseDir $appZipName
$cliZipHash = Assert-Hashed $sums $releaseDir $cliZipName
$pin = (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'codesign.json') | ConvertFrom-Json).certificate_sha256
if ($pin -notmatch '^[0-9a-f]{64}$') { Stop-Render 'packaging/codesign.json carries no usable certificate_sha256' }
$msiPath = Join-Path $releaseDir $msiName

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('bws-feeds-' + [guid]::NewGuid().ToString('N'))
$work = Join-Path (Split-Path -Parent ([System.IO.Path]::GetFullPath($OutDir))) ('.' + (Split-Path -Leaf $OutDir) + '.partial-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
try {
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    # The archives first, so that one of another release is refused by what it holds before its signatures
    # are asked.
    $copies = [ordered]@{}
    foreach ($half in $packaged.Values) {
        $copies["$($half.Program) in $($half.Zip)"] = Expand-Checked (Join-Path $releaseDir $half.Zip) $half.Program $version $scratch
    }

    # Every signature is asked before any is refused, so one refusal names every file that is not ours.
    $faults = [System.Collections.Generic.List[string]]::new()
    $fault = Get-SignatureFault $msiPath $pin
    if ($fault) { $faults.Add("$msiName ($fault)") }
    foreach ($name in $copies.Keys) {
        $fault = Get-SignatureFault $copies[$name] $pin
        if ($fault) { $faults.Add("$name ($fault)") }
    }
    if ($faults.Count) {
        Stop-Render "not signed by the certificate packaging/codesign.json pins ($pin), with a timestamp: $($faults -join ', ')"
    }

    $msiProperties = Read-MsiProperties $msiPath
    if ($msiProperties['ProductVersion'] -ne $version) {
        Stop-Render "$msiName is version '$($msiProperties['ProductVersion'])', and the tag is $version"
    }
    $upgradeCode = "{$($identity.UpgradeCode)}"
    if ($msiProperties['UpgradeCode'] -ne $upgradeCode) {
        Stop-Render "$msiName carries UpgradeCode $($msiProperties['UpgradeCode']), and build-msi.ps1 builds with $upgradeCode"
    }
    # Every build of the installer has a ProductCode of its own, so it comes from the file and from nowhere else.
    $productCode = [string]$msiProperties['ProductCode']
    if ($productCode -notmatch '^\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}$') {
        Stop-Render "$msiName carries no ProductCode WinGet can match on ('$productCode')"
    }

    # The Chocolatey icon. Moderation takes a CDN address and refuses raw.githubusercontent.com and
    # github.com/.../raw alike, and an address on a branch would change the icon of an approved package with
    # every commit, so it is jsDelivr pinned to the tag, and the file has to be in the tag for that to answer.
    # SVG, because the tags carry no PNG: the validator's rule CPMR0058 is "Use PNG or SVG for package icons"
    # (docs.chocolatey.org, read 2026-11-03), and jsDelivr answered 200 image/svg+xml with the 1585 bytes of
    # the file for v0.5.0 that day.
    $iconPath = 'site/assets/icon.svg'
    & git -C $root cat-file -e "$($Tag):$iconPath" 2>$null
    if ($LASTEXITCODE -ne 0) { Stop-Render "$iconPath is not in $Tag in this clone, so the icon address would not answer. Fetch the tag" }
    $repoPath = ([uri]$product.source).AbsolutePath.Trim('/')

    $download = "$($product.source)/releases/download/$Tag"
    $shared = [ordered]@{
        VERSION            = $version
        RELEASE_DATE       = $releaseDate
        WINGET_SCHEMA      = $WingetSchema
        PUBLISHER          = $publisher
        REPO_URL           = $product.source
        ISSUES_URL         = "$($product.source)/issues"
        PROJECT_URL        = $product.homepage
        LICENSE            = $product.license
        LICENSE_URL        = "$($product.source)/blob/$Tag/LICENSE"
        COPYRIGHT          = $product.copyright
        RELEASE_NOTES_URL  = "$($product.source)/releases/tag/$Tag"
        PACKAGE_SOURCE_URL = "$($product.source)/tree/main/packaging/chocolatey"
        ICON_URL           = "https://cdn.jsdelivr.net/gh/$repoPath@$Tag/$iconPath"
    }

    # Every sentence below is one the README makes about the program, held to what the program does. Three
    # are narrower than the README's short list: what should be up and is not is asked the way the window
    # asks it, mismatch:stopped, because start:auto !status:running also answers per-user templates and
    # services a trigger starts, which are stopped where they are meant to be (7 against 1 on 2026-09-23);
    # --json is taken by the commands that read or change a service, and license, tool and tool fix refuse
    # it with exit 2 (bws 0.5.0, 2026-11-03); and --follow-network is the one way off the machine.
    $audit = 'It freezes every service into a snapshot file and later says what changed since, configuration ' +
        'apart from what merely started or stopped, and reads from the System log who changed a start type or ' +
        'installed a service.'
    $offline = 'It works on the local machine and reaches no network unless you pass --follow-network.'
    $elevated = 'Looking needs no administrator rights, and changing a service does.'
    $close = 'Close the window and every bws that is running before an upgrade.'
    # Owner's decision of 2026-11-03: the command keeps its name, and the packages say so.
    $sameName = 'The command is named bws, as is the command of Bitwarden Secrets Manager CLI. With both ' +
        'installed, the one found first on PATH answers, and the machine PATH comes before the PATH of an account.'
    $tags = @('services', 'windows-services', 'services-msc', 'service-manager', 'drivers', 'sysadmin', 'administration', 'audit', 'snapshot', 'diff')

    $packages = @(
        [ordered]@{
            Kind       = 'window'
            WingetId   = 'DonislawDev.BetterWindowsServices'
            ChocoId    = 'better-windows-services'
            Title      = $identity.Name
            Moniker    = 'betterwindowsservices'
            Short      = $identity.Shortcut
            Tags       = $tags + @('gui')
            Body       = ("$($identity.Name) is services.msc rebuilt for people who administer Windows machines - a " +
                'window and a command line over one engine. Ask the machine a question instead of scrolling: ' +
                'mismatch:stopped is what should be up and is not. Every stop, start, restart and start ' +
                'type change is shown as a plan, with the services it takes down with it, before anything runs.' +
                "`n`n$audit It shows what services.msc hides - who signed the binary, triggers, privileges, the " +
                'security descriptor, the process and its memory - and says when it could not read a field instead ' +
                "of showing it empty. $offline")
            WingetHow  = ("The Windows installer puts $($identity.Name) in the Start menu of every account, puts its " +
                'folder - the window and the bws command line - on the machine PATH, and asks for administrator ' +
                "rights. $close With --scope user you get the portable window instead: no administrator rights, " +
                'and no Start menu entry either - start it by typing BetterWindowsServices in a new terminal. The ' +
                "command line alone is the package DonislawDev.BetterWindowsServices.CLI. $elevated $sameName")
            ChocoHow   = ("This package runs the Windows installer of the release. It puts $($identity.Name) in the Start " +
                'menu of every account, and puts its folder - the window and the bws command line - on the machine PATH. ' +
                "$close The command line alone is the package better-windows-services-cli. $elevated $sameName")
            Own        = [ordered]@{
                MSI_URL          = "$download/$msiName"
                MSI_SHA256       = $msiHash.ToUpperInvariant()
                MSI_PRODUCT_CODE = $productCode
                MSI_UPGRADE_CODE = $upgradeCode
                APP_ZIP_URL      = "$download/$appZipName"
                APP_ZIP_SHA256   = $appZipHash.ToUpperInvariant()
                APP_ZIP_EXE      = $appZipExe
            }
            Chocolatey = @('chocolateyinstall', 'chocolateyuninstall')
        },
        [ordered]@{
            Kind       = 'cli'
            WingetId   = 'DonislawDev.BetterWindowsServices.CLI'
            ChocoId    = 'better-windows-services-cli'
            Title      = "$($identity.Name) CLI"
            Moniker    = 'betterwindowsservices-cli'
            Short      = 'Manage and audit Windows services from the command line, with dry runs, snapshots and a diff of what changed'
            Tags       = $tags + @('cli', 'command-line', 'automation')
            Body       = ("bws is the command line of $($identity.Name), for scripts, scheduled tasks and CI. bws list " +
                '--query asks the machine a question in the language of the search box of the window. Every stop, ' +
                'start, restart and start type change is a plan first and can be previewed with --dry-run.' +
                "`n`n$audit Every command that reads or changes a service takes --json, every ending has an exit " +
                "code of its own, and a diff that finds drift ends with 5. $offline")
            WingetHow  = ("Type bws --help in a new terminal to see the commands. $elevated The window, with a Start " +
                "menu entry and the same command line, is the package DonislawDev.BetterWindowsServices. $sameName")
            ChocoHow   = ("Type bws --help in a new terminal to see the commands. $elevated The window, with a Start " +
                "menu entry and the same command line, is the package better-windows-services. $sameName")
            Own        = [ordered]@{
                CLI_ZIP_URL    = "$download/$cliZipName"
                CLI_ZIP_SHA256 = $cliZipHash.ToUpperInvariant()
                CLI_ZIP_EXE    = $cliZipExe
            }
            Chocolatey = @('chocolateyinstall')
        }
    )

    foreach ($package in $packages) {
        $values = [ordered]@{}
        foreach ($key in $shared.Keys) { $values[$key] = $shared[$key] }
        foreach ($key in $package.Own.Keys) { $values[$key] = $package.Own[$key] }
        $values['WINGET_ID'] = $package.WingetId
        $values['CHOCO_ID'] = $package.ChocoId
        $values['TITLE'] = $package.Title
        $values['MONIKER'] = $package.Moniker
        $values['SHORT_DESCRIPTION'] = $package.Short
        $values['DESCRIPTION'] = $package.Body
        $values['WINGET_HOW_TO_START'] = $package.WingetHow
        $values['CHOCO_HOW_TO_START'] = $package.ChocoHow
        $values['WINGET_TAGS'] = ($package.Tags | ForEach-Object { "- $_" }) -join "`n"
        $values['CHOCO_TAGS'] = $package.Tags -join ' '
        foreach ($key in $values.Keys) {
            $multiLine = $key -in @('DESCRIPTION', 'WINGET_TAGS')
            $value = [string]$values[$key]
            if (-not $value) { Stop-Render "the $($package.Kind) package has no value for $key" }
            if ($unsafe.IsMatch($value) -or (-not $multiLine -and $value.Contains("`n"))) {
                Stop-Render "the value of $key for the $($package.Kind) package would break a file it lands in: $value"
            }
        }

        # The WinGet folder is the path the files take in microsoft/winget-pkgs: the identifier's first letter,
        # then one folder per part of the identifier, then the version.
        $idParts = $package.WingetId.Split('.')
        $wingetDir = Join-Path $work (Join-Path 'winget/manifests' (Join-Path $idParts[0].Substring(0, 1).ToLowerInvariant() (($idParts -join '/') + "/$version")))
        $files = [ordered]@{
            (Join-Path $wingetDir "$($package.WingetId).yaml")                = Join-Path $wingetTemplates 'version.yaml.in'
            (Join-Path $wingetDir "$($package.WingetId).locale.en-US.yaml")   = Join-Path $wingetTemplates 'locale.en-US.yaml.in'
            (Join-Path $wingetDir "$($package.WingetId).installer.yaml")      = Join-Path $wingetTemplates "installer.$($package.Kind).yaml.in"
            (Join-Path $work "chocolatey/$($package.ChocoId)/$($package.ChocoId).nuspec") = Join-Path $chocoTemplates 'package.nuspec.in'
        }
        foreach ($script in $package.Chocolatey) {
            $files[(Join-Path $work "chocolatey/$($package.ChocoId)/tools/$script.ps1")] = Join-Path $chocoTemplates "tools/$script.$($package.Kind).ps1.in"
        }

        $used = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($target in $files.Keys) {
            $template = $files[$target]
            if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { Stop-Render "there is no template at $template" }
            Write-Rendered $target (Expand-Template $template $values $used)
        }
        # A value nobody asks for is a template that lost a line, which rendering would otherwise not notice.
        $unused = @($values.Keys | Where-Object { -not $used.Contains($_) })
        if ($unused) { Stop-Render "no template of the $($package.Kind) package asks for $($unused -join ', ')" }
    }

    # A rename, which fails when the destination is there. Move-Item would put the folder INSIDE an existing
    # one, so a second run given the same -OutDir, past the check at the start while this one rendered,
    # would nest its packages in this run's and still say rendered.
    try {
        [System.IO.Directory]::Move($work, $OutDir)
    }
    catch {
        Stop-Render "$OutDir could not be made from the rendered folder, so nothing was put there: $(Get-Reason $_)"
    }
    $work = $null
}
finally {
    if ($work -and (Test-Path -LiteralPath $work)) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Output "build-feeds: $Tag rendered into $OutDir"
Get-ChildItem -LiteralPath $OutDir -Recurse -File | ForEach-Object { Write-Output ('  ' + $_.FullName.Substring(([System.IO.Path]::GetFullPath($OutDir)).Length + 1)) }
