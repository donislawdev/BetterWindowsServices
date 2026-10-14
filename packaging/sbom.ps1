#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Write an SPDX 2.3 bill of materials for one packaged archive, from the curated register.

.DESCRIPTION
    Reads packaging/components.json - the register of everything this product ships that
    somebody else wrote - and renders one SPDX document for one package. The register is the
    SOURCE. This script invents nothing: the licences and the list of components come from the
    register, the exact versions of the runtime packs come from the build manifest the publish
    produced, and the sha256 comes from the archive on disk.

    WHY THE LIST IS NOT TAKEN FROM THE ARCHIVE, which is the obvious alternative. Both programs
    publish as a SINGLE self-contained file, so the archive holds one executable and two text
    files. Everything else - WPF, the .NET runtime, WPF-UI - is inside that executable with no
    package metadata left anywhere. A scanner over the release would report the two files it can
    see and assign a licence to neither.

    WHAT THIS SCRIPT REFUSES, rather than papering over:

      * a package id the register does not know;
      * a component the register puts in this package and the build manifest does not carry;
      * a component the build manifest carries and the register does not name - the direction
        that matters, because it is the one that appears when a dependency arrives by itself;
      * a version literal in the register that disagrees with the build;
      * a document that would not validate: a duplicate identifier, an identifier with a
        character the format forbids, a relationship pointing at nothing, a missing required
        field, or a LicenseRef used and never defined.

    ONE DOCUMENT PER ARCHIVE, not one for both. The two packages have different contents and a
    single document describing both would misstate each of them.

    AND ONE FOR THE INSTALLER, from 2026-10-14. The installer carries both packages (the register's
    `installer.contents`), so its document lists the components of both - each package checked in
    both directions against ITS OWN build manifest, exactly as its archive's document is, and a
    component both carry has to have resolved to the same version in both. Nothing here keeps a
    second list of what the installer holds.

    The document carries the archive's own sha256, so it is tied to exact bytes. That is also why
    it sits BESIDE the archive in the release rather than inside it: a file cannot contain its
    own hash.

.PARAMETER PackageId
    Which file to describe: a key of the register's `packages` object (cli or gui), or `installer`.

.PARAMETER ZipPath
    The file that was built for it - an archive, or the installer. Its sha256 goes into the
    document.

.PARAMETER DepsPath
    For a package: the .deps.json the publish produced for it. This is where the resolved version
    of every runtime pack is read from. Ask MSBuild for it with -getProperty:ProjectDepsFilePath
    rather than assembling the path by hand - build-dist.ps1 does exactly that.

.PARAMETER DepsFolder
    For the installer: the folder holding `<archive>.deps.json` for every package it carries, the
    names build-dist.ps1 writes them under and the names phase A hands them to phase B under.

.PARAMETER OutPath
    Where to write the SPDX JSON.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackageId,
    [Parameter(Mandatory)] [string] $ZipPath,
    [string] $DepsPath = '',
    [string] $DepsFolder = '',
    [Parameter(Mandatory)] [string] $OutPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$registerPath = Join-Path $PSScriptRoot 'components.json'

function Assert-File([string] $path, [string] $why) {
    if (-not (Test-Path -LiteralPath $path)) { throw "sbom: missing '$path' - $why" }
}

Assert-File $registerPath 'the register is the source of this document'
Assert-File $ZipPath 'build the package before describing it'

$register = Get-Content -Raw -LiteralPath $registerPath | ConvertFrom-Json
$knownPackages = @($register.packages.PSObject.Properties.Name)

# What is described, which packages it carries, and where each package's build manifest is. For an
# archive that is the archive's own package and the manifest passed in. For the installer it is every
# package under `contents`, each with the manifest named after its archive.
$manifests = [ordered]@{}
if ($PackageId -eq 'installer') {
    if (-not $register.PSObject.Properties['installer']) { throw 'sbom: the register names no installer' }
    if (-not $DepsFolder) { throw 'sbom: the installer is described with -DepsFolder, the folder holding each package''s <archive>.deps.json' }
    $described = $register.installer
    $fileName = [string]$described.file
    foreach ($id in @($described.contents)) {
        if ($knownPackages -notcontains $id) {
            throw "sbom: the installer carries '$id', which is no package of the register - it knows: $($knownPackages -join ', ')"
        }
        $manifests[$id] = Join-Path $DepsFolder ($register.packages.$id.zip + '.deps.json')
    }
    if ($manifests.Count -eq 0) { throw 'sbom: the installer carries no package, which cannot be right' }
}
else {
    if ($knownPackages -notcontains $PackageId) {
        throw "sbom: the register has no package '$PackageId' - it knows: $($knownPackages -join ', '), installer"
    }
    if (-not $DepsPath) { throw "sbom: package '$PackageId' is described with -DepsPath, the build manifest of its publish" }
    $described = $register.packages.$PackageId
    $fileName = [string]$described.zip
    $manifests[$PackageId] = $DepsPath
}
foreach ($id in $manifests.Keys) {
    Assert-File $manifests[$id] 'the publish writes it, and the runtime pack versions are read from it'
}

# The product version, read from the one file that sets it rather than retyped. Rule 11 of
# CLAUDE.md makes that number the owner's to move, and a second copy here would be a first place
# to forget.
$props = Get-Content -Raw -LiteralPath (Join-Path $root 'Directory.Build.props')
if ($props -notmatch '<Version>([^<]+)</Version>') {
    throw 'sbom: cannot read <Version> from Directory.Build.props'
}
$productVersion = $Matches[1]

$zipSha = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$zipItem = Get-Item -LiteralPath $ZipPath

# ---------------------------------------------------------------------------------------------
# What the build actually resolved
# ---------------------------------------------------------------------------------------------

# Every library one publish recorded, as name -> version. The manifest spells a runtime pack
# `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.12` and a package `WPF-UI/4.3.0`, so
# the prefix is stripped here and the register's `kind` says which shape to expect.
# Ordinal-ignore-case: NuGet ids are compared without case everywhere else in this repository.
function Read-Resolved([string] $path) {
    $deps = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    $resolved = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($library in $deps.libraries.PSObject.Properties) {
        $parts = $library.Name -split '/', 2
        if ($parts.Count -ne 2) { throw "sbom: '$($library.Name)' in $path is not <name>/<version>" }
        # A project is ours - this product's own assemblies. They are not third-party components and
        # must not be demanded of the register.
        if ($library.Value.type -eq 'project') { continue }
        $resolved[($parts[0] -replace '^runtimepack\.', '')] = $parts[1]
    }
    # The comma keeps the dictionary whole. Without it PowerShell unrolls it into its entries on the
    # way out, and the caller gets an array of pairs instead of a map (trap 20 of docs/14).
    return , $resolved
}

# ---------------------------------------------------------------------------------------------
# The register, checked against each package's build in both directions
# ---------------------------------------------------------------------------------------------

$named = [System.Collections.Generic.HashSet[string]]::new(
    [string[]] @($register.components | ForEach-Object { $_.name }), [System.StringComparer]::OrdinalIgnoreCase)

$mine = [System.Collections.Generic.List[object]]::new()
$versions = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($id in $manifests.Keys) {
    $resolved = Read-Resolved $manifests[$id]
    $inPackage = @($register.components | Where-Object { $_.in -contains $id })
    if ($inPackage.Count -eq 0) {
        throw "sbom: the register lists no component in package '$id', which cannot be right"
    }

    foreach ($component in $inPackage) {
        $declared = $component.PSObject.Properties.Name -contains 'version'
        $fromBuild = $component.PSObject.Properties.Name -contains 'version_from'
        if ($declared -eq $fromBuild) {
            throw ("sbom: '$($component.name)' must carry exactly one of `version` and `version_from` - " +
                'a component whose version we choose states it, one the SDK resolves does not')
        }
        if (-not $resolved.ContainsKey($component.name)) {
            throw ("sbom: the register puts '$($component.name)' in package '$id' and the build " +
                "did not resolve it. Either it stopped shipping - take it out of the register - or the " +
                "name drifted. What the build carries: $(($resolved.Keys | Sort-Object) -join ', ')")
        }
        $built = $resolved[$component.name]
        if ($declared -and $component.version -ne $built) {
            throw ("sbom: the register says '$($component.name)' is $($component.version) and the build " +
                "resolved $built. Update the register - a document describing a different build than " +
                'the one in the archive is worse than no document.')
        }
        # One component in two packages of one installer is one entry in its document, so it has to
        # be one version. Two would mean the two publishes resolved different runtimes, and that is a
        # question about the build, not a line this document can write.
        if ($versions.ContainsKey($component.name)) {
            if ($versions[$component.name] -ne $built) {
                throw ("sbom: '$($component.name)' resolved to $($versions[$component.name]) in one package " +
                    "and to $built in '$id'. One installer carries one copy of it - look at how the two " +
                    'packages were built.')
            }
            continue
        }
        $versions[$component.name] = $built
        $mine.Add($component)
    }

    # THE DIRECTION THAT FINDS THINGS. A component we chose is in a project file and hard to forget.
    # One that arrives as somebody else's dependency arrives with no prompt at all, ships, and creates
    # exactly the same licence obligation - WPF-UI.Abstractions is in this register because a person
    # noticed, not because anything asked.
    $unnamed = @($resolved.Keys | Where-Object { -not $named.Contains($_) })
    if ($unnamed) {
        throw ("sbom: the build of '$id' carries $($unnamed.Count) component(s) the register does not " +
            "name: $(($unnamed | Sort-Object) -join ', '). Read the licence in the package on disk - not the " +
            'label on its listing - add it to packaging/components.json and to THIRD-PARTY-NOTICES.md.')
    }
    # And named for the right package. A component listed against the other package only would pass
    # the sweep above and be missing from this document.
    $inNames = [System.Collections.Generic.HashSet[string]]::new(
        [string[]] @($inPackage | ForEach-Object { $_.name }), [System.StringComparer]::OrdinalIgnoreCase)
    $misfiled = @($resolved.Keys | Where-Object { -not $inNames.Contains($_) })
    if ($misfiled) {
        throw ("sbom: the build of '$id' carries $(($misfiled | Sort-Object) -join ', '), which the " +
            "register names but does not list in this package. Add '$id' to its `in` list.")
    }
}

# ---------------------------------------------------------------------------------------------
# The document
# ---------------------------------------------------------------------------------------------

# SPDX identifiers accept letters, digits, '.' and '-' and nothing else, so
# `Microsoft.NETCore.App.Runtime.win-x64` survives and anything else is rewritten. Collisions
# after the rewrite are checked rather than assumed away: two components sharing one identifier
# would produce a document that says different things about the same element.
function ConvertTo-SpdxId([string] $name) {
    return "SPDXRef-Package-$($name -replace '[^A-Za-z0-9.\-]', '-')"
}

function Get-Purl($component, [string] $version) {
    switch ($component.kind) {
        'nuget' { "pkg:nuget/$($component.name)@$version" }
        'dotnet-runtime-pack' { "pkg:nuget/$($component.name)@$version" }
        # A component on no registry has no package URL, and saying nothing is the honest answer -
        # a made-up one resolves to somebody else's package.
        default { $null }
    }
}

# The name a reader sees for the file. An archive is named by its stem, as every document before the
# installer was. The installer's stem is the window archive's own (BetterWindowsServices-win-x64), and two
# documents naming one thing would mislead, so it keeps its extension - the same answer Chrono Mock gives.
$stem = if ([System.IO.Path]::GetExtension($fileName) -eq '.zip') {
    [System.IO.Path]::GetFileNameWithoutExtension($fileName)
}
else {
    $fileName
}
$rootId = ConvertTo-SpdxId $stem
$packages = [System.Collections.Generic.List[object]]::new()
$relationships = [System.Collections.Generic.List[object]]::new()
$usedRefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

$packages.Add([ordered]@{
        SPDXID           = $rootId
        name             = $stem
        versionInfo      = $productVersion
        downloadLocation = "$($register.product.source)/releases/download/v$productVersion/$fileName"
        homepage         = $register.product.homepage
        filesAnalyzed    = $false
        licenseConcluded = $register.product.license
        licenseDeclared  = $register.product.license
        copyrightText    = $register.product.copyright
        supplier         = $register.product.supplier
        checksums        = @([ordered]@{ algorithm = 'SHA256'; checksumValue = $zipSha })
        comment          = $described.description
    })
$relationships.Add([ordered]@{
        spdxElementId      = 'SPDXRef-DOCUMENT'
        relationshipType   = 'DESCRIBES'
        relatedSpdxElement = $rootId
    })

# Ordinal, because SPDX identifiers are case-sensitive and a case-insensitive map would report a
# collision the format does not have.
$seen = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)

foreach ($component in $mine) {
    $id = ConvertTo-SpdxId $component.name
    if ($seen.ContainsKey($id)) {
        throw "sbom: '$($component.name)' and '$($seen[$id])' both become $id once made an SPDX identifier"
    }
    $seen[$id] = $component.name

    foreach ($licence in @($component.license_declared, $component.license_concluded)) {
        if ($licence -like 'LicenseRef-*') { [void] $usedRefs.Add($licence) }
    }

    $entry = [ordered]@{
        SPDXID           = $id
        name             = $component.name
        versionInfo      = $versions[$component.name]
        downloadLocation = $component.source
        filesAnalyzed    = $false
        licenseConcluded = $component.license_concluded
        licenseDeclared  = $component.license_declared
        # The register records who holds the copyright rather than the full notice. The notices
        # themselves are reproduced in THIRD-PARTY-NOTICES.md, which ships inside the archive -
        # the document comment below points a reader there.
        copyrightText    = $component.supplier
        supplier         = "Organization: $($component.supplier)"
    }

    $purl = Get-Purl $component $versions[$component.name]
    if ($purl) {
        $entry.externalRefs = @([ordered]@{
                referenceCategory = 'PACKAGE-MANAGER'
                referenceType     = 'purl'
                referenceLocator  = $purl
            })
    }

    # A pinned file is stated in the COMMENT, and never in `checksums`, which is where the first
    # version of this put it. In SPDX a package's `checksums` is the hash of the PACKAGE FILE -
    # for a component that arrives from NuGet that is the .nupkg - so a hash of one DLL inside it
    # is a false statement that anybody checking the entry against the package would catch, and
    # phase C would have attested it. Found by the review of PR #5.
    #
    # NOT moved into an SPDX `File` element either, which is the other repair the review offered.
    # A File wants a SHA1 beside the SHA256 in SPDX 2.x, this register carries only the SHA256,
    # and inventing a second hash to satisfy a schema would be a worse answer than a sentence. The
    # pin is enforced by ComponentRegisterGuards and again by build-dist.ps1 before packing - the
    # document records it, it does not police it.
    if ($component.PSObject.Properties.Name -contains 'files') {
        $pinned = ($component.files | ForEach-Object { "$($_.path) sha256 $($_.sha256)" }) -join '; '
        $entry.comment = "This build pins the bytes of: $pinned"
    }

    $packages.Add($entry)
    $relationships.Add([ordered]@{
            spdxElementId      = $rootId
            relationshipType   = 'CONTAINS'
            relatedSpdxElement = $id
        })
}

$created = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$document = [ordered]@{
    spdxVersion       = 'SPDX-2.3'
    dataLicense       = 'CC0-1.0'
    SPDXID            = 'SPDXRef-DOCUMENT'
    name              = "$stem-$productVersion"
    # Unique per build, because it carries the hash of the exact archive this document describes.
    documentNamespace = "$($register.product.homepage)/spdx/$stem/$productVersion/$($zipSha.Substring(0, 16))"
    creationInfo      = [ordered]@{
        created  = $created
        creators = @("Person: $(($register.product.supplier -split ':\s*', 2)[-1])", 'Tool: bws-sbom-1')
        comment  = ('Rendered from packaging/components.json, a register maintained by hand and checked ' +
            'in both directions against the .deps.json this publish produced. Not the output of a ' +
            'scanner over the built package: both programs ship as a single self-contained file, so a ' +
            'scan of the archive would find two executables and no package metadata at all.')
    }
    comment           = ('Full licence texts for every component listed here are in ' +
        'THIRD-PARTY-NOTICES.md, which ships inside the archive. `bws license --components` prints ' +
        'the same set from inside the program, for a machine with no internet.')
    packages          = $packages
    relationships     = $relationships
}

if ($usedRefs.Count -gt 0) {
    $extracted = [System.Collections.Generic.List[object]]::new()
    foreach ($ref in ($usedRefs | Sort-Object)) {
        $known = $register.license_refs.PSObject.Properties.Name
        if ($known -notcontains $ref) {
            throw "sbom: '$ref' is used by a component and defined nowhere in the register's license_refs"
        }
        $definition = $register.license_refs.$ref
        $extracted.Add([ordered]@{
                licenseId     = $ref
                name          = $definition.name
                extractedText = "See $($definition.url)"
                seeAlsos      = @($definition.url)
            })
    }
    $document.hasExtractedLicensingInfos = $extracted
}

# ---------------------------------------------------------------------------------------------
# A self-check before anything is written
# ---------------------------------------------------------------------------------------------
# This document goes into a release and gets an attestation of its own. A malformed SBOM is worse
# than no SBOM: it looks like an answer.

$ids = @($packages | ForEach-Object { $_.SPDXID })
$unique = [System.Collections.Generic.HashSet[string]]::new([string[]] $ids, [System.StringComparer]::Ordinal)
if ($unique.Count -ne $ids.Count) { throw 'sbom: two packages share an SPDX identifier' }

foreach ($id in $ids) {
    if (($id -replace '^SPDXRef-', '') -notmatch '^[A-Za-z0-9.\-]+$') {
        throw "sbom: identifier '$id' has a character the format does not allow"
    }
}
foreach ($relationship in $relationships) {
    foreach ($end in @($relationship.spdxElementId, $relationship.relatedSpdxElement)) {
        if ($end -ne 'SPDXRef-DOCUMENT' -and -not $unique.Contains($end)) {
            throw "sbom: a relationship points at '$end', which is in no package of this document"
        }
    }
}
foreach ($entry in $packages) {
    foreach ($field in @('SPDXID', 'name', 'versionInfo', 'downloadLocation', 'licenseConcluded',
            'licenseDeclared', 'copyrightText')) {
        if (-not $entry.$field) { throw "sbom: package '$($entry.name)' has no $field, which SPDX requires" }
    }
}

$json = $document | ConvertTo-Json -Depth 12

# Written without a byte order mark. A BOM makes the file fail some SPDX validators, and it is
# the same trap that has turned other things in this repository red before.
[System.IO.File]::WriteAllText($OutPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ("== sbom == {0}: {1} component(s), archive {2:N1} MB, sha256 {3}..." -f `
        $fileName, $mine.Count, ($zipItem.Length / 1MB), $zipSha.Substring(0, 12))
foreach ($component in $mine) {
    Write-Host ("   {0} {1}  {2}" -f $component.name, $versions[$component.name], $component.license_concluded)
}
