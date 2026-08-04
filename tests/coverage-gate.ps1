# The coverage ratchet ADR-10 promised and nobody built.
#
# IN THE REPOSITORY RATHER THAN IN tools/, AND THAT IS THE ONE STRUCTURAL DECISION HERE.
# Everything else this project uses to check itself - the mutation registry, the document bridge
# - lives outside version control and therefore cannot run in CI. A coverage floor kept there
# would have needed a second copy of this comparison written into the workflow, and two copies of
# one rule is the failure this project keeps paying for. So the gate ships with the tests, CI
# runs it directly, and tools/check.ps1 calls the same file. One implementation, two callers.
#
# WHY IT LOOKS LIKE THIS. ADR-10 fixed the policy years before the number: a threshold will
# exist, its value comes from the FIRST REAL MEASUREMENT rather than from a figure somebody
# liked, it rises with coverage, and it is never lowered to turn a red build green. Lowering is
# the owner's decision, not tidying. That is the size ratchet's shape, applied to a different
# measurement, and this script is deliberately built the same way.
#
# WHAT THE FIRST MEASUREMENT ACTUALLY SAID, on 2026-08-04, and none of it was guessable:
#
#   1. The first number to come out was 56%, and it was wrong. Four coverage reports anchor
#      their paths on two different roots - three on src\ and one on src\Bws.Core\ - so the same
#      source file was counted as two files and its coverage split between them.
#   2. 532 sequence points belong to generated P/Invoke code under obj\. Nobody wrote it and
#      nobody should be measured on it.
#   3. Bws.Cli did not appear at all. Every test for it ran the built executable as a separate
#      process, so nothing instrumented it - 3135 lines, about a quarter of the shipped code,
#      invisible to any gate. A test project was created the same day and the real figure turned
#      out to be 9.2%. Turning "invisible" into "measured and ratcheted" is the whole point.
#
# TWO SCOPES, AND THEY ARE TWO DIFFERENT MEASUREMENTS RATHER THAN ONE NUMBER IN TWO PLACES:
#
#   everything  all five test projects, including the ones that read this machine's service
#               control manager. The figure that means something.
#   anywhere    only the projects a runner can run. CI can carry this one, and it is lower by
#               nineteen points on Bws.Core because the integration tests do that much of the
#               work.
#
# Both floors live in ONE file in the repository - tests\coverage-floor.json - because CI has no
# access to tools/ and a threshold kept in two places is a threshold that will disagree with
# itself. The file is the contract, this script and the workflow are two readers of it.
#
# MEASURED BEFORE TRUSTING IT: three consecutive runs of Bws.Core.Tests gave 2934 points and
# 2103 covered, identically. The property tests draw random seeds, so this was worth checking -
# a gate that wobbles teaches everybody to ignore it.
#
# WHAT THIS DOES NOT TELL YOU, and it is the reason backlog 62 exists: coverage says what was
# EXECUTED, never what was CHECKED. A test with no assertions covers everything it touches. The
# answer to "was it checked" is the mutation registry, and this does not replace it.

[CmdletBinding()]
param(
    [ValidateSet('everything', 'anywhere')]
    [string]$Scope = 'everything',

    # Write the floors from what was just measured. Raises only - lowering is refused and says
    # so, because ADR-10 makes that the owner's decision rather than a repair.
    [switch]$Mark,

    # The one way to lower a floor, and it has to be typed out. ADR-10: never lowered to turn a
    # red build green.
    [switch]$Lower,

    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$floorFile = Join-Path $PSScriptRoot 'coverage-floor.json'

$projects = @{
    everything = @(
        'tests\Bws.Core.Tests', 'tests\Bws.Architecture.Tests', 'tests\Bws.Gui.Tests',
        'tests\Bws.Cli.Tests', 'tests\Bws.Integration.Tests')

    # The integration project is the only one left out, and for the reason the workflow already
    # gives: it reads this machine's manager, compares against this machine's sc.exe and holds
    # budgets measured on this machine's processors.
    anywhere = @(
        'tests\Bws.Core.Tests', 'tests\Bws.Architecture.Tests', 'tests\Bws.Gui.Tests',
        'tests\Bws.Cli.Tests')
}

# ---------------------------------------------------------------------------------------

function Measure-Coverage([string[]] $Projects) {
    $results = Join-Path ([IO.Path]::GetTempPath()) ("bws-coverage-" + [guid]::NewGuid().ToString('N'))

    foreach ($project in $Projects) {
        # ALWAYS Debug, INCLUDING ON A RUNNER THAT JUST BUILT Release, and the extra build is the
        # price of the floor meaning the same thing everywhere. Optimisation changes which
        # sequence points exist, so a floor measured in one configuration and checked in the other
        # would drift for a reason that has nothing to do with the tests.
        & dotnet test (Join-Path $root $project) -c Debug --nologo `
            --collect:"XPlat Code Coverage" --results-directory $results 2>&1 | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "$project did not pass. Coverage of a red suite says nothing."
        }
    }

    $hits = @{}

    foreach ($report in Get-ChildItem $results -Recurse -Filter 'coverage.cobertura.xml') {
        [xml]$xml = Get-Content $report.FullName
        $source = @($xml.coverage.sources.source)[0]

        # A report with no source root carries no classes either - one of the four is always like
        # this. Skipping it silently would be fine and saying so costs nothing.
        if (-not $source) { continue }

        foreach ($package in $xml.coverage.packages.package) {
            foreach ($class in @($package.classes.class)) {
                # THE PATHS ARE MADE ABSOLUTE BEFORE THEY ARE COMPARED. Without this, the same
                # file arrives under two names from two reports and its coverage is split
                # between them - which is how the first measurement came out at 56% instead of
                # 90%.
                $relative = ([IO.Path]::GetFullPath((Join-Path $source $class.filename))).Replace("$root\", '')

                # Generated interop. Not ours to be measured on.
                if ($relative -match '\\obj\\') { continue }

                foreach ($line in @($class.lines.line)) {
                    if ($null -eq $line) { continue }

                    $key = "$relative|$($line.number)"
                    if (-not $hits.ContainsKey($key)) { $hits[$key] = 0 }
                    $hits[$key] += [int]$line.hits
                }
            }
        }
    }

    Remove-Item $results -Recurse -Force -ErrorAction SilentlyContinue

    $measured = @{}

    foreach ($group in ($hits.Keys | Group-Object { ($_ -split '\|')[0].Split('\')[1] })) {
        $covered = @($group.Group | Where-Object { $hits[$_] -gt 0 }).Count

        $measured[$group.Name] = [pscustomobject]@{
            Points = $group.Count
            Covered = $covered
            Percent = [math]::Floor(1000 * $covered / $group.Count) / 10
        }
    }

    # A floor, like every other sweep in this directory: an empty measurement would clear every
    # threshold at once and read exactly like a pass.
    if ($measured.Count -lt 2) {
        throw "Coverage came back for $($measured.Count) assemblies. That is the measurement failing, not the product shrinking."
    }

    return $measured
}

function Read-Floors {
    if (-not (Test-Path $floorFile)) { return @{} }

    $floors = @{}

    foreach ($scope in (Get-Content $floorFile -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties) {
        $inner = @{}
        foreach ($assembly in $scope.Value.PSObject.Properties) { $inner[$assembly.Name] = [double]$assembly.Value }
        $floors[$scope.Name] = $inner
    }

    return $floors
}

# ---------------------------------------------------------------------------------------

if ($SelfTest) {
    # The comparison, not the measurement. Running the suite twice to prove an inequality would
    # cost four minutes to check arithmetic - what has to be proved is that a drop is REFUSED and
    # a rise is not, and that is decided here.
    $floors = @{ 'Bws.Core' = 90.0; 'Bws.Gui' = 80.0 }

    $cases = @(
        @{ Name = 'a drop of one tenth is refused'; Measured = @{ 'Bws.Core' = 89.9; 'Bws.Gui' = 80.0 }; Expect = 1 },
        @{ Name = 'standing still is allowed'; Measured = @{ 'Bws.Core' = 90.0; 'Bws.Gui' = 80.0 }; Expect = 0 },
        @{ Name = 'a rise is allowed'; Measured = @{ 'Bws.Core' = 95.0; 'Bws.Gui' = 80.0 }; Expect = 0 },
        @{ Name = 'an assembly that vanished is refused'; Measured = @{ 'Bws.Core' = 95.0 }; Expect = 1 }
    )

    $wrong = 0

    Write-Host "Self test - the comparison a floor is:"
    Write-Host ""

    foreach ($case in $cases) {
        $below = 0

        foreach ($assembly in $floors.Keys) {
            if (-not $case.Measured.ContainsKey($assembly) -or $case.Measured[$assembly] -lt $floors[$assembly]) {
                $below++
            }
        }

        $got = if ($below -gt 0) { 1 } else { 0 }

        # NOT $mark, and that cost ten minutes. Variable names ignore case in PowerShell, so
        # $mark IS the -Mark parameter of this script - typed [switch] - and assigning a string
        # to it fails at the assignment with a message about SwitchParameter that names no line.
        # The block ran perfectly when lifted into a file with no param() block, which is what
        # made it worth writing down.
        $verdict = if ($got -eq $case.Expect) { 'ok    ' } else { 'WRONG ' }
        if ($got -ne $case.Expect) { $wrong++ }

        Write-Host ("  {0} {1}" -f $verdict, $case.Name)
    }

    Write-Host ""
    Write-Host ("Self test: {0} of {1} decisions correct." -f ($cases.Count - $wrong), $cases.Count)

    exit $(if ($wrong -eq 0) { 0 } else { 1 })
}

# ---------------------------------------------------------------------------------------

Write-Host ("scope                {0}" -f $Scope)
Write-Host ("projects             {0}" -f ($projects[$Scope].Count))
Write-Host ""

$measured = Measure-Coverage $projects[$Scope]
$floors = Read-Floors

if ($Mark) {
    $existing = if ($floors.ContainsKey($Scope)) { $floors[$Scope] } else { @{} }
    $lowered = @()
    $next = @{}

    foreach ($assembly in ($measured.Keys | Sort-Object)) {
        $now = $measured[$assembly].Percent

        if ($existing.ContainsKey($assembly) -and $now -lt $existing[$assembly]) {
            $lowered += "{0}: {1}% -> {2}%" -f $assembly, $existing[$assembly], $now
        }

        $next[$assembly] = $now
    }

    if ($lowered.Count -gt 0 -and -not $Lower) {
        Write-Host "REFUSING TO WRITE. These would go down:" -ForegroundColor Red
        foreach ($line in $lowered) { Write-Host ("  {0}" -f $line) -ForegroundColor Red }
        Write-Host ""
        Write-Host "ADR-10: a floor is never lowered to turn a red build green. Lowering one is" -ForegroundColor Red
        Write-Host "the owner's decision, and -Lower is how it is said out loud." -ForegroundColor Red
        exit 1
    }

    $floors[$Scope] = $next
    $floors | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $floorFile -Encoding UTF8

    Write-Host ("floors written       {0} for scope '{1}'" -f $next.Count, $Scope)
    foreach ($assembly in ($next.Keys | Sort-Object)) { Write-Host ("  {0,-10} {1}%" -f $assembly, $next[$assembly]) }
    exit 0
}

if (-not $floors.ContainsKey($Scope)) {
    Write-Host "No floor for this scope yet." -ForegroundColor Yellow
    foreach ($assembly in ($measured.Keys | Sort-Object)) {
        Write-Host ("  {0,-10} {1,5} points {2,6}%" -f $assembly, $measured[$assembly].Points, $measured[$assembly].Percent)
    }
    Write-Host ""
    Write-Host ("  powershell -File tests\coverage-gate.ps1 -Scope {0} -Mark" -f $Scope)
    exit 2
}

$floor = $floors[$Scope]
$below = 0

foreach ($assembly in ($floor.Keys | Sort-Object)) {
    if (-not $measured.ContainsKey($assembly)) {
        Write-Host ("  GONE       {0} - the floor names it and the measurement does not" -f $assembly) -ForegroundColor Red
        $below++
        continue
    }

    $now = $measured[$assembly]
    $verdict = if ($now.Percent -lt $floor[$assembly]) { 'BELOW ' } else { 'ok    ' }
    $colour = if ($now.Percent -lt $floor[$assembly]) { 'Red' } else { 'Green' }
    if ($now.Percent -lt $floor[$assembly]) { $below++ }

    Write-Host ("  {0} {1,-10} {2,5} points  {3,6}%  floor {4}%" -f
        $verdict, $assembly, $now.Points, $now.Percent, $floor[$assembly]) -ForegroundColor $colour
}

foreach ($assembly in ($measured.Keys | Sort-Object)) {
    if (-not $floor.ContainsKey($assembly)) {
        Write-Host ("  NEW        {0} at {1}% - run -Mark to put a floor under it" -f $assembly, $measured[$assembly].Percent) -ForegroundColor Yellow
    }
}

Write-Host ""

if ($below -gt 0) {
    Write-Host ("below the floor      {0}" -f $below) -ForegroundColor Red
    Write-Host ""
    Write-Host "A floor only ever goes up. If the drop is deliberate, say so with -Mark -Lower," -ForegroundColor Red
    Write-Host "which is the owner's decision rather than a repair." -ForegroundColor Red
    exit 1
}

Write-Host "below the floor      0" -ForegroundColor Green
Write-Host ""
Write-Host "Coverage says what was EXECUTED, never what was CHECKED - a test with no assertions"
Write-Host "covers everything it touches. That question belongs to the mutation registry."
exit 0
