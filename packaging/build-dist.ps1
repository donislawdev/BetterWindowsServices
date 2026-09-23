#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build both release packages: publish, stage, check the pinned bytes, pack, describe, sum.

.DESCRIPTION
    One command that produces exactly what a release publishes, so that the workflow on a tag and
    a person at their own machine run the SAME steps rather than two spellings of them.

    In order:

      1. publishes each package the way README.md promises it - one self-contained file, x64;
      2. checks the sha256 of every third-party binary the register PINS against the package it
         came from. Only the binaries nobody else signs are pinned, and the reasoning for that
         line is in packaging/components.json;
      3. stages the executable with LICENSE and THIRD-PARTY-NOTICES.md beside it, because the
         licences on the borrowed code require their notices to travel with the binary;
      4. RUNS the command line program out of the staging folder - `--version` and `license` -
         because a file that exists is not a file that runs, and publishing exits zero either
         way. The window is not run: it would open a window and not come back;
      5. packs each folder into its archive;
      6. writes an SPDX document beside each archive, which is where the register is checked in
         both directions against the build manifest;
      7. writes SHA256SUMS over everything.

    WHAT IT DOES NOT DO: sign, upload, tag, or publish anything. The release ritual is four
    phases and this is the first half of the first one - see docs/02, ADR-28.

    THE SUMS AND DOCUMENTS IT WRITES DESCRIBE UNSIGNED BYTES. On a real release the archives are
    repacked after signing, which changes every hash, and phase B regenerates both. They are
    written here so that a local run is complete and checkable on its own.

.PARAMETER OutputDirectory
    Where the archives go. Default: dist/ at the repository root.

.PARAMETER PackageId
    Build only one package (cli or gui). Default: both.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [string] $PackageId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$registerPath = Join-Path $PSScriptRoot 'components.json'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }

$register = Get-Content -Raw -LiteralPath $registerPath | ConvertFrom-Json
$ids = @($register.packages.PSObject.Properties.Name)
if ($PackageId) {
    if ($ids -notcontains $PackageId) { throw "build-dist: no package '$PackageId' - the register knows $($ids -join ', ')" }
    $ids = @($PackageId)
}

# Everything that travels beside the executable, from the repository root. The licences on the
# borrowed code are the reason this list is not empty: MIT asks for its notice to be included in
# every copy, and GPL asks for its own text.
$Alongside = @('LICENSE', 'THIRD-PARTY-NOTICES.md')

# The publish properties, written once. Two copies of these would be one copy that eventually
# says something else - and the deps.json path asked of MSBuild below only matches the publish if
# it is asked with the SAME properties.
$Publish = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true')

function Invoke-Step([string[]] $Command) {
    Write-Host "  `$ $($Command -join ' ')"
    & $Command[0] @($Command[1..($Command.Length - 1)])
    if ($LASTEXITCODE -ne 0) { throw "build-dist: '$($Command[0])' failed with exit $LASTEXITCODE" }
}

function Get-Sha256([string] $path) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# Where NuGet put the packages. Asked of the tool rather than assembled from a profile path,
# because a build agent moves it with NUGET_PACKAGES and a guess would quietly check nothing.
function Get-PackageRoot {
    $line = dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) { throw 'build-dist: cannot ask dotnet where the global packages are' }
    $folder = ($line | Select-Object -First 1) -replace '^[^:]*:\s*', ''
    $folder = $folder.Trim()
    if (-not (Test-Path -LiteralPath $folder)) { throw "build-dist: the global package folder '$folder' is not there" }
    return $folder
}

# The build manifest for a project, from MSBuild rather than from a path put together by hand.
# The shape of that path is the SDK's business and it has moved before.
function Get-DepsPath([string] $project) {
    $answer = dotnet build (Join-Path $root $project) -getProperty:ProjectDepsFilePath `
        -p:Configuration=Release -p:RuntimeIdentifier=win-x64 -p:SelfContained=true
    if ($LASTEXITCODE -ne 0) { throw "build-dist: cannot ask MSBuild for the deps.json path of $project" }
    $path = ($answer | Where-Object { $_ -and $_.Trim() } | Select-Object -Last 1).Trim()
    if (-not (Test-Path -LiteralPath $path)) { throw "build-dist: MSBuild named '$path' and there is no such file" }
    return $path
}

Write-Host "building into $OutputDirectory"
if (Test-Path -LiteralPath $OutputDirectory) { Remove-Item -LiteralPath $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$staging = Join-Path $OutputDirectory 'staging'
$packageRoot = Get-PackageRoot

$written = [System.Collections.Generic.List[string]]::new()

foreach ($id in $ids) {
    $package = $register.packages.$id
    Write-Host "`n== $id == $($package.zip)"

    $folder = Join-Path $staging $package.folder
    New-Item -ItemType Directory -Path $folder -Force | Out-Null

    Write-Host "`n[1/6] publishing"
    $out = Join-Path $OutputDirectory "publish-$id"
    Invoke-Step (@('dotnet', 'publish', (Join-Path $root $package.project)) + $Publish + @('-o', $out))

    $executable = Join-Path $out $package.executable
    if (-not (Test-Path -LiteralPath $executable)) {
        throw ("build-dist: the publish produced no $($package.executable). One file per program is what " +
            'section S9 of the specification promises, so this is a broken publish rather than a naming question.')
    }

    Write-Host "`n[2/6] the pinned bytes"
    $deps = Get-DepsPath $package.project
    $resolved = @{}
    foreach ($library in (Get-Content -Raw -LiteralPath $deps | ConvertFrom-Json).libraries.PSObject.Properties) {
        $parts = $library.Name -split '/', 2
        $resolved[($parts[0] -replace '^runtimepack\.', '')] = $parts[1]
    }
    $pinned = 0
    foreach ($component in $register.components) {
        if ($component.in -notcontains $id) { continue }
        if ($component.PSObject.Properties.Name -notcontains 'files') { continue }
        if (-not $resolved.ContainsKey($component.name)) { continue }
        $version = $resolved[$component.name]
        foreach ($file in $component.files) {
            # The package id is lower cased in the folder layout NuGet writes, and the version is
            # taken from the build rather than from the register so that this check follows a bump
            # to the file that moved rather than to the one the register still names.
            $onDisk = Join-Path $packageRoot (Join-Path $component.name.ToLowerInvariant() (Join-Path $version $file.package_path))
            if (-not (Test-Path -LiteralPath $onDisk)) {
                throw ("build-dist: the register pins $($file.package_path) of $($component.name) $version and " +
                    "there is no such file at '$onDisk'. A pin that cannot find its file is a check that passes " +
                    'by reading nothing, so this stops here.')
            }
            $actual = Get-Sha256 $onDisk
            if ($actual -ne $file.sha256) {
                throw ("build-dist: $($component.name) $version does not hash to what the register pins.`n" +
                    "  pinned: $($file.sha256)`n  actual: $actual`n" +
                    'Either the version moved and the register has not - update it from the package on disk - or ' +
                    'somebody replaced a binary on this machine. Nothing has been packed.')
            }
            Write-Host "  ok  $($component.name) $version  $($file.path)"
            $pinned++
        }
    }
    if ($pinned -eq 0) { Write-Host '  nothing in this package carries a pin' }

    Write-Host "`n[3/6] staging"
    Copy-Item -LiteralPath $executable -Destination $folder
    foreach ($name in $Alongside) {
        $source = Join-Path $root $name
        if (-not (Test-Path -LiteralPath $source)) { throw "build-dist: there is no $name at the repository root" }
        Copy-Item -LiteralPath $source -Destination $folder
    }
    Write-Host "  $($package.folder)/ carries $((Get-ChildItem -LiteralPath $folder).Count) files"

    Write-Host "`n[4/6] does it actually run"
    if ($id -eq 'cli') {
        $staged = Join-Path $folder $package.executable
        $version = & $staged --version
        if ($LASTEXITCODE -ne 0) { throw "build-dist: $($package.executable) --version exited $LASTEXITCODE" }
        Write-Host "  --version -> $($version | Select-Object -First 1)"

        # The licence notice has to find the register INSIDE the executable, which is the shape no
        # test on a checkout exercises - a single-file publish is where an embedded resource goes
        # missing without a word.
        #
        # JOINED INTO ONE STRING FIRST, AND THE FIRST VERSION OF THIS LINE DID NOT. A native
        # program hands PowerShell an ARRAY of lines, and `-notmatch` over an array does not
        # answer yes or no - it returns every element that does not match, which in an `if` is a
        # non-empty collection and therefore true. So the check failed on a program whose output
        # named all five components, on the first real run of this script. Measured here rather
        # than reasoned about: docs/14 has the family.
        $licence = (& $staged license --components) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "build-dist: $($package.executable) license --components exited $LASTEXITCODE" }
        foreach ($component in $register.components) {
            if ($component.in -notcontains $id) { continue }
            if ($licence -notmatch [regex]::Escape($component.name)) {
                throw "build-dist: the packaged program does not name $($component.name) in its licence notice"
            }
        }
        Write-Host "  license --components names every component of this package"
    }
    else {
        Write-Host '  not run: it would open a window and not come back'
    }

    Write-Host "`n[5/6] packing"
    $zip = Join-Path $OutputDirectory $package.zip
    Compress-Archive -Path $folder -DestinationPath $zip -Force
    $written.Add($zip)
    Write-Host ("  {0}  {1:N1} MB" -f $package.zip, ((Get-Item -LiteralPath $zip).Length / 1MB))

    Write-Host "`n[6/6] bill of materials"
    $sbom = Join-Path $OutputDirectory ($package.zip + '.spdx.json')
    # No $LASTEXITCODE check after this. Called with `&` the script runs in this runspace, so
    # that variable holds whatever the last NATIVE command inside it set rather than the script's
    # own outcome - and every failure in sbom.ps1 is a throw, which propagates here and stops
    # this script. Trap 4 of docs/14, caught by tools/lint.ps1.
    & (Join-Path $PSScriptRoot 'sbom.ps1') -PackageId $id -ZipPath $zip -DepsPath $deps -OutPath $sbom
    $written.Add($sbom)

    # THE BUILD MANIFEST TRAVELS WITH THE ARCHIVE, and it is the seam to the card machine.
    #
    # Phase B signs the executable, which changes the archive, which changes its sha256 - so the
    # document written a moment ago describes bytes nobody will ship and has to be written again
    # over the signed ones. It cannot be: the resolved version of every runtime pack lives in this
    # manifest, which exists only where the publish happened.
    #
    # It is NOT a release asset. It goes into the workflow artifact beside the archives, phase B
    # reads it, and that is the end of it.
    Copy-Item -LiteralPath $deps -Destination (Join-Path $OutputDirectory ($package.zip + '.deps.json'))

    Remove-Item -LiteralPath $out -Recurse -Force
}

$sums = Join-Path $OutputDirectory 'SHA256SUMS'
$lines = $written | ForEach-Object { "{0}  {1}" -f (Get-Sha256 $_), (Split-Path -Leaf $_) }
[System.IO.File]::WriteAllText($sums, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "`n== built =="
Get-ChildItem -LiteralPath $OutputDirectory -File | ForEach-Object { "  {0,14:N0}  {1}" -f $_.Length, $_.Name }
Write-Host ''
Write-Host 'These executables are NOT signed and these sums are over unsigned bytes.'
Write-Host 'On a release, phase B signs them on the card and writes both again.'
