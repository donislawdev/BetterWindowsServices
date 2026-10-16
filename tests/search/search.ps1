<#
.SYNOPSIS
    The weekly search: every random property run for minutes instead of a fraction of a second.

.DESCRIPTION
    Built 2026-10-16 on the owner's decision (project note docs\PROJEKT-FUZZING-20261016.md,
    outside the repository). The model is the weekly fuzzing job of the owner's other project,
    Testing Files Generator: never on a push, five minutes a target, and a finding kept so that it
    becomes a regression test instead of disappearing with the machine.

    WHAT IS SEARCHED. Every test carrying [Trait("search", "weekly")] in a test project that
    references CsCheck. Each of them samples through tests\Searching.cs, which SearchGuards in the
    architecture tests holds, so the list asked of dotnet test is the list of properties.

    HOW A PROPERTY IS MADE TO SEARCH. CsCheck reads CsCheck_Time whenever a test passes no time of
    its own, and then samples for that many seconds instead of its iteration count - measured
    2026-10-16 on CsCheck 4.7.0, so the tests do not change between a pull request and a Sunday.
    Each property runs alone in its own dotnet test, so it has the whole machine for its budget and
    a hang or a crash costs that one property rather than the part.

    WHAT IT SAYS ABOUT EACH PROPERTY, read from the trx file rather than from the console, which
    dotnet prints in the language of the machine:

      SEARCHED        passed, and tests\Searching.cs counted calls
      FAILED          the property did not hold - the seed and what the property printed are kept
      NOT SEARCHED    passed and counted nothing, which is a green test that searched nothing
      NOT RUN         the filter matched no test - the list and the tree disagree
      DID NOT FINISH  no result and a failing exit - a crash, a stack overflow or a hang, which is
                      a finding too, and the console is kept for it

    Only SEARCHED is green. A part that cannot see a failure is worse than no part, so before it
    trusts its own reading every part runs SearchCanaryTests with BWS_SEARCH_CANARY=1 and expects
    FAILED with a seed. Anything else stops the part.

    A FINDING IS NOT FIXED HERE. The seed goes into the property as seed: "..." in a test of its
    own, so every pull request replays it, and then the code is fixed. Without that commit the
    finding leaves with the runner.

.PARAMETER Seconds
    The budget of each property. 300 on Sunday, the owner's decision. A pull request touching the
    search itself runs it with a few seconds, which proves the wiring and searches nothing much.

.PARAMETER PlanTo
    Appends properties=, parts= and shards= to this file (GitHub's step outputs) and stops.

.PARAMETER Shard
    i/k: run part i of k, i counted from zero. Property n of the sorted list goes to part n mod k.

.PARAMETER Json
    Where this part writes its verdicts, rewritten after every property so that a part killed in
    the middle still leaves what it saw.

.PARAMETER Only
    A fragment of the property names to run, for a search by hand on one machine.

.PARAMETER Report
    A folder of part results. Puts them together, writes the table to the job summary and is red
    unless every part reported, every listed property was SEARCHED and every canary went red.

.PARAMETER SelfTest
    Builds and runs nothing. Proves the cut into parts, the reading of a trx file and the verdicts
    on cases whose answer is known, including the ones that must come out red.

.EXAMPLE
    pwsh -File tests\search\search.ps1 -Seconds 20 -Only QueryCompletion
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)][string] $Root = '',
    [int] $Seconds = 300,
    [int] $PerPart = 6,
    [int] $MostParts = 16,
    [string] $Configuration = 'Release',
    [string] $PlanTo = '',
    [string] $Shard = '',
    [string] $Json = '',
    [string] $Only = '',
    [string] $Report = '',
    [int] $Properties = -1,
    [int] $Parts = -1,
    [switch] $NoBuild,
    [switch] $NoCanary,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

if ($Root.Length -eq 0) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }

$CanaryTest = 'Bws.Core.Tests.SearchCanaryTests.A_property_that_fails_is_red_and_says_how_to_reproduce_it'
$CanaryProject = 'tests\Bws.Core.Tests'
$CanarySeconds = 5

# --- the parts --------------------------------------------------------------------------------

# One part per six properties at five minutes each is about half an hour a part, and never more
# parts than properties. None for none - an empty matrix is an error on GitHub.
function Get-PartCount([int] $Count) {
    if ($Count -le 0) { return 0 }
    $parts = [Math]::Min($MostParts, [int] [Math]::Ceiling($Count / $PerPart))
    return [Math]::Max(1, [Math]::Min($parts, $Count))
}

function Get-ShardList([int] $Count) {
    if ($Count -le 0) { return '[]' }
    return '[' + ((0..($Count - 1)) -join ',') + ']'
}

function Select-Part($All, [int] $Part, [int] $Of) {
    $list = @($All)
    for ($n = 0; $n -lt $list.Count; $n++) {
        if ($n % $Of -eq $Part) { $list[$n] }
    }
}

# --- reading what a run said -------------------------------------------------------------------

# The results of one trx file. Namespaces ignored on purpose - the schema URI is the 2010 one on
# every SDK this project has used, and a guard against it changing would be the reading below
# finding nothing, which comes out as NOT RUN or DID NOT FINISH rather than as green.
function Read-Trx([string] $Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return [pscustomobject]@{ Results = 0; Outcome = ''; Message = '' }
    }

    $xml = [xml] $Text
    $results = @($xml.SelectNodes("//*[local-name()='UnitTestResult']"))

    if ($results.Count -eq 0) {
        return [pscustomobject]@{ Results = 0; Outcome = ''; Message = '' }
    }

    $failed = @($results | Where-Object { $_.outcome -ne 'Passed' })
    $first = if ($failed.Count) { $failed[0] } else { $results[0] }
    $message = $first.SelectSingleNode(".//*[local-name()='Message']")

    return [pscustomobject]@{
        Results = $results.Count
        Outcome = if ($failed.Count) { [string] $failed[0].outcome } else { 'Passed' }
        Message = if ($message) { [string] $message.InnerText } else { '' }
    }
}

# The calls tests\Searching.cs counted, one line per sampling, added up.
function Read-Calls([string] $Text) {
    $total = [long] 0
    foreach ($match in [regex]::Matches([string] $Text, '(?m)^calls=(\d+)\b')) {
        $total += [long] $match.Groups[1].Value
    }
    return $total
}

# The seed CsCheck prints with a failure: Set seed: "..." or -e CsCheck_Seed=... to reproduce.
function Get-Seed([string] $Message) {
    $match = [regex]::Match([string] $Message, 'Set seed: "([^"]+)"')
    if ($match.Success) { return $match.Groups[1].Value }
    return ''
}

function Get-Verdict($Trx, [long] $Calls, [int] $ExitCode) {
    if ($Trx.Results -eq 0) {
        if ($ExitCode -eq 0) { return 'NOT RUN' }
        return 'DID NOT FINISH'
    }
    if ($Trx.Outcome -ne 'Passed') { return 'FAILED' }
    if ($Calls -le 0) { return 'NOT SEARCHED' }
    return 'SEARCHED'
}

# --- the report ---------------------------------------------------------------------------------

# Every reason the run is not green, as sentences. Empty means green.
function Get-Problems($Results, [int] $Expected, [int] $PartCount) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $seen = @{}

    foreach ($part in 0..([Math]::Max(0, $PartCount - 1))) {
        if ($PartCount -le 0) { break }
        $one = @($Results | Where-Object { $_.part -eq $part })
        if ($one.Count -eq 0) { $problems.Add("part $part never reported"); continue }
        if (-not $one[0].finished) { $problems.Add("part $part did not finish") }
        if (-not $one[0].canary -or $one[0].canary.verdict -ne 'FAILED' -or -not $one[0].canary.seed) {
            $problems.Add("part $part never showed it can see a failure (canary: $($one[0].canary.verdict))")
        }
        foreach ($property in @($one[0].properties)) {
            $seen[$property.name] = $true
            if ($property.verdict -ne 'SEARCHED') {
                $problems.Add(("{0}: {1}{2}" -f $property.name, $property.verdict, $(if ($property.seed) { " - seed $($property.seed)" } else { '' })))
            }
        }
    }

    if ($seen.Count -ne $Expected) {
        $problems.Add("$($seen.Count) properties reported against $Expected listed")
    }

    return $problems.ToArray()
}

function Write-Summary($Results, $Problems) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('## What the weekly search found')
    $lines.Add('')
    $lines.Add('| property | verdict | calls | seconds | seed |')
    $lines.Add('|---|---|---|---|---|')

    foreach ($part in @($Results | Sort-Object part)) {
        foreach ($property in @($part.properties)) {
            $lines.Add(("| {0} | {1} | {2} | {3} | {4} |" -f $property.name, $property.verdict, $property.calls, $property.seconds, $property.seed))
        }
    }

    $lines.Add('')
    if ($Problems.Count -eq 0) {
        $lines.Add('Every listed property was searched and none failed. Every part showed it can see a failure.')
    } else {
        $lines.Add('**Not green:**')
        foreach ($problem in $Problems) { $lines.Add("- $problem") }
    }

    $lines | ForEach-Object { Write-Output $_ }

    if ($env:GITHUB_STEP_SUMMARY) {
        [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
    }
}

# --- the self test ------------------------------------------------------------------------------

if ($SelfTest) {
    $checks = 0

    function Assert-Same([string] $What, $Expected, $Actual) {
        if ("$Expected" -ne "$Actual") { throw "self-test: $What gave '$Actual', expected '$Expected'" }
        $script:checks++
    }

    Assert-Same 'parts for 0,1,6,7,16,200' '0,1,1,2,3,16' (@(0, 1, 6, 7, 16, 200 | ForEach-Object { Get-PartCount $_ }) -join ',')
    Assert-Same 'shards for 3' '[0,1,2]' (Get-ShardList 3)
    Assert-Same 'shards for 0' '[]' (Get-ShardList 0)

    $names = 1..16 | ForEach-Object { "p$_" }
    $cut = 0..2 | ForEach-Object { @(Select-Part $names $_ 3) }
    Assert-Same 'parts cover every property once' (($names | Sort-Object) -join ',') (($cut | Sort-Object) -join ',')
    Assert-Same 'part 1 of 3' 'p2,p5,p8,p11,p14' ((Select-Part $names 1 3) -join ',')

    $ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    $red = "<TestRun xmlns='$ns'><Results><UnitTestResult testName='a' outcome='Failed'><Output><ErrorInfo><Message>CsCheck.CsCheckException : Set seed: `"2QNxRkDfTLZ2`" or -e CsCheck_Seed=2QNxRkDfTLZ2 to reproduce (12 shrinks, 4 skipped, 9 total).&#10;the canary drew 1000</Message></ErrorInfo></Output></UnitTestResult></Results></TestRun>"
    $green = "<TestRun xmlns='$ns'><Results><UnitTestResult testName='a' outcome='Passed' /></Results></TestRun>"
    $none = "<TestRun xmlns='$ns'><ResultSummary outcome='Completed'><Counters total='0' /></ResultSummary></TestRun>"

    $readRed = Read-Trx $red
    Assert-Same 'red trx outcome' 'Failed' $readRed.Outcome
    Assert-Same 'seed from a red trx' '2QNxRkDfTLZ2' (Get-Seed $readRed.Message)
    Assert-Same 'green trx outcome' 'Passed' (Read-Trx $green).Outcome
    Assert-Same 'results in an empty trx' 0 (Read-Trx $none).Results
    Assert-Same 'results in no trx at all' 0 (Read-Trx '').Results
    Assert-Same 'seed from a message without one' '' (Get-Seed 'Assert.True() Failure')

    Assert-Same 'calls over two samplings' 55279041 (Read-Calls "calls=55279031 seconds=2.0`r`ncalls=10 seconds=0.1`r`n")
    Assert-Same 'calls with no report' 0 (Read-Calls '')

    Assert-Same 'verdict green and counted' 'SEARCHED' (Get-Verdict (Read-Trx $green) 5 0)
    Assert-Same 'verdict green and not counted' 'NOT SEARCHED' (Get-Verdict (Read-Trx $green) 0 0)
    Assert-Same 'verdict red' 'FAILED' (Get-Verdict $readRed 3 1)
    Assert-Same 'verdict no result and a clean exit' 'NOT RUN' (Get-Verdict (Read-Trx $none) 0 0)
    Assert-Same 'verdict no result and a failing exit' 'DID NOT FINISH' (Get-Verdict (Read-Trx '') 0 1)

    $good = { param($p) [pscustomobject]@{ name = $p; verdict = 'SEARCHED'; calls = 9; seconds = 1; seed = '' } }
    $planted = [pscustomobject]@{ verdict = 'FAILED'; seed = 'x' }
    $whole = @(
        [pscustomobject]@{ part = 0; finished = $true; canary = $planted; properties = @((& $good 'a'), (& $good 'c')) },
        [pscustomobject]@{ part = 1; finished = $true; canary = $planted; properties = @((& $good 'b')) })
    Assert-Same 'report on a whole green run' 0 @(Get-Problems $whole 3 2).Count

    Assert-Same 'report on a missing part' 'part 1 never reported|2 properties reported against 3 listed' ((Get-Problems @($whole[0]) 3 2) -join '|')

    $blind = @([pscustomobject]@{ part = 0; finished = $true; canary = [pscustomobject]@{ verdict = 'SEARCHED'; seed = '' }; properties = @((& $good 'a')) })
    Assert-Same 'report on a canary that stayed green' 'part 0 never showed it can see a failure (canary: SEARCHED)' ((Get-Problems $blind 1 1) -join '|')

    $found = @([pscustomobject]@{ part = 0; finished = $true; canary = $planted; properties = @([pscustomobject]@{ name = 'a'; verdict = 'FAILED'; calls = 3; seconds = 1; seed = 'zz' }) })
    Assert-Same 'report on a finding' 'a: FAILED - seed zz' ((Get-Problems $found 1 1) -join '|')

    Write-Output "self-test: $checks checks, every one as expected"
    exit 0
}

# --- putting the parts together -----------------------------------------------------------------

if ($Report.Length -gt 0) {
    if ($Properties -lt 0 -or $Parts -lt 0) { throw '-Report needs -Properties and -Parts, the numbers the plan gave' }

    $results = @(Get-ChildItem -LiteralPath $Report -Recurse -Filter 'search-*.json' -ErrorAction SilentlyContinue |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })

    $problems = @(Get-Problems $results $Properties $Parts)
    Write-Summary $results $problems

    if ($problems.Count -gt 0) { exit 1 }
    exit 0
}

# --- listing ------------------------------------------------------------------------------------

$env:DOTNET_CLI_UI_LANGUAGE = 'en'

# The test projects that can hold a property: the ones referencing CsCheck. Read from the project
# files rather than listed here, so a project that gains a property is searched without an edit.
$projects = @(Get-ChildItem -LiteralPath (Join-Path $Root 'tests') -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } |
    Where-Object { Select-String -LiteralPath (Join-Path $_.FullName "$($_.Name).csproj") -Pattern 'Include="CsCheck"' -Quiet } |
    ForEach-Object { "tests\$($_.Name)" } | Sort-Object)

if ($projects.Count -eq 0) { throw 'No test project references CsCheck, so there is nothing to search.' }

if (-not $NoBuild) {
    foreach ($project in $projects) {
        $said = & dotnet build (Join-Path $Root $project) -c $Configuration --nologo 2>&1
        if ($LASTEXITCODE -ne 0) {
            $said | ForEach-Object { Write-Output "    $_" }
            throw "$project did not build, so nothing was searched."
        }
    }
}

$listed = foreach ($project in $projects) {
    $said = & dotnet test (Join-Path $Root $project) -c $Configuration --no-build --nologo --list-tests --filter 'search=weekly' 2>&1
    if ($LASTEXITCODE -ne 0) {
        $said | ForEach-Object { Write-Output "    $_" }
        throw "$project would not list its tests."
    }
    foreach ($line in $said) {
        if ("$line" -match '^\s+(Bws\.\S+)\s*$') { [pscustomobject]@{ Project = $project; Name = $Matches[1] } }
    }
}

$listed = @($listed | Sort-Object Name)
if ($Only.Length -gt 0) { $listed = @($listed | Where-Object { $_.Name -like "*$Only*" }) }

if ($listed.Count -eq 0) { throw "No property carries [Trait(`"search`", `"weekly`")] in $($projects -join ', ')$(if ($Only) { " matching '$Only'" }). Nothing to search is a broken list, not a quiet week." }

if ($PlanTo.Length -gt 0) {
    $count = Get-PartCount $listed.Count
    $lines = @("properties=$($listed.Count)", "parts=$count", "shards=$(Get-ShardList $count)")
    [IO.File]::AppendAllText($PlanTo, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
    $listed | ForEach-Object { Write-Output "  $($_.Name)" }
    $lines | ForEach-Object { Write-Output "plan: $_" }
    exit 0
}

$partNumber = 0
$partCount = 1
if ($Shard.Length -gt 0) {
    if ($Shard -notmatch '^(\d+)/(\d+)$' -or [int] $Matches[2] -lt 1 -or [int] $Matches[1] -ge [int] $Matches[2]) {
        throw "-Shard takes i/k with i counted from zero and below k, and was given '$Shard'"
    }
    $partNumber = [int] $Matches[1]
    $partCount = [int] $Matches[2]
}

$mine = @(Select-Part $listed $partNumber $partCount)
Write-Output ("part {0} of {1}: {2} of {3} properties, {4} s each." -f $partNumber, $partCount, $mine.Count, $listed.Count, $Seconds)

# --- searching ----------------------------------------------------------------------------------

$work = Join-Path ([IO.Path]::GetTempPath()) ('bws-search-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force -Path $work

function Invoke-Property([string] $Project, [string] $Name, [int] $Budget, [bool] $Planted) {
    $slug = ($Name -replace '[^A-Za-z0-9]+', '-')
    $trx = Join-Path $work "$slug.trx"
    $calls = Join-Path $work "$slug.calls"

    $env:CsCheck_Time = "$Budget"
    $env:BWS_SEARCH_REPORT = $calls
    $env:BWS_SEARCH_CANARY = if ($Planted) { '1' } else { $null }

    # A hang ends as a property that did not finish rather than as a part that never reports. The
    # margin is generous - the budget is how long CsCheck samples, not how long one call may take.
    $hang = '{0}s' -f ($Budget + 600)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $said = & dotnet test (Join-Path $Root $Project) -c $Configuration --no-build --nologo `
        --filter "FullyQualifiedName=$Name" --logger "trx;LogFileName=$slug.trx" --results-directory $work `
        --blame-hang-timeout $hang --blame-hang-dump-type none 2>&1
    $exit = $LASTEXITCODE
    $took = [Math]::Round($watch.Elapsed.TotalSeconds, 1)

    $env:CsCheck_Time = $null
    $env:BWS_SEARCH_REPORT = $null
    $env:BWS_SEARCH_CANARY = $null

    $read = Read-Trx $(if (Test-Path -LiteralPath $trx) { Get-Content -LiteralPath $trx -Raw } else { '' })
    $counted = Read-Calls $(if (Test-Path -LiteralPath $calls) { Get-Content -LiteralPath $calls -Raw } else { '' })
    $verdict = Get-Verdict $read $counted $exit

    $result = [ordered]@{
        project = $Project
        name    = $Name
        verdict = $verdict
        calls   = $counted
        seconds = $took
        seed    = Get-Seed $read.Message
    }

    # What the property said, kept whenever it was not green - rule 15 of the project: the first red
    # run is the one that may not come back.
    if ($verdict -ne 'SEARCHED') {
        $result.message = ($read.Message -split "`r?`n" | Select-Object -First 40) -join "`n"
        $result.console = (@($said | ForEach-Object { "$_" }) | Select-Object -Last 60) -join "`n"
    }

    return [pscustomobject] $result
}

$state = [ordered]@{
    part       = $partNumber
    parts      = $partCount
    seconds    = $Seconds
    started    = (Get-Date).ToUniversalTime().ToString('o')
    finished   = $false
    canary     = $null
    properties = [System.Collections.Generic.List[object]]::new()
}

function Save-State {
    if ($Json.Length -gt 0) {
        [IO.File]::WriteAllText($Json, ($state | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))
    }
}

if (-not $NoCanary) {
    $state.canary = Invoke-Property $CanaryProject $CanaryTest $CanarySeconds $true
    Save-State
    Write-Output ("canary: {0}, seed '{1}'" -f $state.canary.verdict, $state.canary.seed)

    if ($state.canary.verdict -ne 'FAILED' -or -not $state.canary.seed) {
        Write-Output $state.canary.console
        throw 'The canary did not come back red with a seed, so this part cannot see a failure and searched nothing.'
    }
}

foreach ($one in $mine) {
    $result = Invoke-Property $one.Project $one.Name $Seconds $false
    $state.properties.Add($result)
    Save-State
    Write-Output ("{0,-15} {1,14:N0} calls {2,7} s  {3}{4}" -f $result.verdict, $result.calls, $result.seconds, $result.name, $(if ($result.seed) { "  seed $($result.seed)" } else { '' }))
    if ($result.verdict -ne 'SEARCHED' -and $result.message) { $result.message -split "`n" | ForEach-Object { Write-Output "    | $_" } }
}

$state.finished = $true
$state.finishedAt = (Get-Date).ToUniversalTime().ToString('o')
Save-State

$red = @($state.properties | Where-Object { $_.verdict -ne 'SEARCHED' })
Write-Output ("{0} of {1} searched, {2} not." -f ($mine.Count - $red.Count), $mine.Count, $red.Count)

if ($red.Count -gt 0) { exit 1 }
exit 0
