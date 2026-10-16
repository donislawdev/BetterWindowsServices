# Proves the guards can fail.
#
# A green test run is not evidence. The sentence that is evidence is "this test would be red
# if I broke that", and the only way to know it is to break that and watch. This project has
# been doing exactly that by hand since the first slice - and every one of those proofs died
# with the session that made it. Nothing here is repeatable, so nothing here is checkable
# twice.
#
# Borrowed from the Testing Files Generator project, which runs 54 of these. The method
# transfers, none of its decisions do.
#
#     pwsh -File tests/mutation/mutate.ps1              # all of them
#     pwsh -File tests/mutation/mutate.ps1 -Only order   # ones whose name contains "order"
#     pwsh -File tests/mutation/mutate.ps1 -SelfTest     # the runner's own canaries, builds nothing
#
# THE LIST IS IN entries.ps1 BESIDE THIS FILE, and both have been in version control since
# 2026-10-16 so that GitHub can run them - .github/workflows/mutation.yml, every entry once a week
# in sixteen parts and the entries a change reaches on every pull request. Until that day this
# file held the list as well, outside the repository, and a full pass ran on one machine, rarely:
# the last one before the move was 2026-09-25, at 767 entries of what had become 1228.
#
# HOW CI USES THIS FILE. The plan job asks -PlanTo how many entries a change reaches and how many
# parts to cut them into, and builds nothing. Each part runs -Shard i/k with -CiScope and writes
# -Json, and tests/mutation/report.ps1 puts the parts together. On a pull request the whole thing
# is a report and never red - a decision of 2026-10-16, until the runner has earned trust on those
# machines - and the weekly run is the gate.
#
# KILLING THIS RUN LEAVES ONE MUTATED FILE ON DISK, and it is in a file your session probably
# never opened. Measured 2026-09-23: a full gate was stopped part way through this step and
# src\Bws.Gui\FilterRow.xaml was left carrying Focusable="True" where the source says "False".
# It compiles, it looks like nothing, and it would have gone into a commit. The restore happens
# after the test run rather than in a trap, so an interrupt between the two writes wins.
#
# So: after stopping this, ALWAYS run `git status` and look for a modified file you did not
# touch, then `git checkout --` it. That is the whole check, it takes one command, and it is
# the only thing standing between an interrupted gate and a mutation in the history.
#
# Three outcomes per entry, and the third one is why this file is longer than it looks:
#
#   caught   the named test went red. The guard works.
#   MISSED   the named test stayed green. The guard does not check what it claims to.
#   BROKEN   the mutated tree does not compile, so the run proves nothing either way.
#
#   BASELINE RED   the named test is red on the UNMUTATED tree, so a red run under the mutant
#                  would prove nothing. Counted as not proved, and the mutant is not even built.
#
# "caught" WAS ONLY HALF OF THE PROOF UNTIL 2026-09-29, AND BASELINE RED IS THE OTHER HALF. Nothing
# ran the named test on the unmutated tree first, so a test red for another reason - a fixture
# that no longer builds, a style moved to a file that loads later, a sentence that only an
# elevated session sees - was reported caught under every mutant. Found 2026-09-29 (W6): styles
# moved to Themes/Plan.xaml after the last narrow test run, and 28 of 28 caught said nothing about
# whether the new tests were green. Now, after the canary, the clean tree is built once and every
# named test of the selection runs once, forty names to a run - backlog 477. The cost is one build
# and one test run per forty distinct names. -SelfTest plants a red test in an xUnit output and
# checks the entry naming it comes out BASELINE RED.
#
# BROKEN exists because of a trap the borrowed project fell into and wrote down: a build
# failure also returns a non-zero code, so a runner that only looks at "did the test command
# fail" reports a non-compiling tree as a caught mutation. Four of their forty five entries
# were silently in that state. Here the build is a separate step with its own verdict.
#
#   NOT HERE   only under -CiScope: the named test is one CI does not run, so it is not judged
#              on that machine at all. Listed on its own and never counted as a failure - it is
#              still judged by a run on a developer machine. Since 2026-10-16.

# TWO TRAPS PAID FOR, 2026-08-02. Both cost a run, both are invisible until they happen.
#
# 1. KILLING THIS SCRIPT LEAVES THE TREE MUTATED. The original is put back in a finally
#    block, and a killed process does not run one. If a run is interrupted, check
#    `git status` and `git diff` before believing anything the tree says - the file will
#    still compile and the tests will still be green, because the mutation is by definition
#    a small legal edit.
#
# 1a. AND IT LEAVES THE BINARIES MUTATED EVEN WHEN IT FINISHES CLEANLY. The sources are put
#    back, `git status` is clean, and bin/ still holds whatever the last entry compiled. A
#    `dotnet test --no-build` after a run therefore tests a mutation and reports a failure that
#    looks exactly like a regression - it did, on 2026-08-02, four times running, in a guard
#    about row identity that was never broken. This script now rebuilds at the end. If that
#    rebuild ever fails, it says so loudly, because the alternative is silence and a lie.
#
# 2. A MUTATION CAN HANG THE TESTS INSTEAD OF REDDENING THEM. Removing a guard against
#    overlapping work made a test wait forever on a gate rather than fail, and a run that
#    never ends reports nothing at all. The fix belongs in the TEST, not here: anything
#    asserting that a call comes straight back has to time out and fail, never simply await.
#    This project has paid for the same lesson once before, choosing a regex engine.

[CmdletBinding()]
param(
    [string] $Root = '',
    [string] $Only = '',

    # ONLY THE ENTRIES ANCHORED IN FILES THIS CHANGE TOUCHED. Added 2026-09-05.
    #
    # WHY, IN ONE MEASUREMENT. The full registry is 2022 of the 2425 seconds a complete
    # tools/check.ps1 takes - eighty-three per cent of the gate in one step, read out of
    # tools/check-logs/20260901-133731 onwards and last from 20260903-143500. That cost is the
    # reason CLAUDE.md puts the full run once per package rather than once per slice, and the
    # side effect of that cadence is the thing this switch is for: for the whole package,
    # nobody knows whether the guard on the behaviour just changed still fails when it should.
    #
    # The registry holds 357 entries spread over 121 distinct files. A slice touching two files
    # is five to fifteen entries, which is under two minutes rather than thirty-four.
    #
    # MEASURED THE DAY IT WAS BUILT, rather than estimated. `-Touched -Since HEAD~3` on this
    # tree: eleven changed files, NINE of 357 entries selected, and the whole run - canary,
    # nine entries, closing rebuild - took 119 seconds. Against 2022 for the registry, that is
    # seventeen times cheaper for this particular change, and the ratio grows as the registry
    # does while a slice keeps touching the same handful of files.
    #
    # THIS IS A LOOP TOOL AND NEVER A GATE. A change in file A can disarm a guard anchored in
    # file B - CLAUDE.md records exactly that happening on 2026-08-12, when moving setters into
    # a base class blinded an existing guard while the whole suite stayed green. Before saying
    # done, the full run is the only run.
    [switch] $Touched,

    # What -Touched compares against. Anything git understands. HEAD by default, which means
    # "everything not committed yet", including files nobody has staged.
    [string] $Since = 'HEAD',

    # Skips the canary. Only for debugging this script itself.
    [switch] $NoCanary,

    # Proves the baseline check can fail, without building anything: a planted xUnit output with
    # a red test in it has to turn the entry naming that test into BASELINE RED. Backlog 477.
    # Since 2026-10-16 it also proves the selection a pull request gets, the cut into parts and
    # the reading of the runs=anywhere trait, on cases whose answer is known before the run.
    [switch] $SelfTest,

    # THE ENTRIES A CHANGE REACHES, compared with this base - anything git understands, and the
    # workflow passes the base commit of the pull request. Select-Reached below says which three
    # ways an entry is reached, and why anything unclear selects every entry. Since 2026-10-16.
    [string] $Base = '',

    # ONE PART OF THE SELECTION, written i/k: part i of k, counted from zero. Entry n of the
    # selection goes to part n mod k, so every part works out its own share from the same commit
    # and nothing has to be passed between machines.
    [string] $Shard = '',

    # Appends entries=, parts= and shards= to this file - GitHub's step outputs - prints what was
    # selected and why, and stops. Builds nothing.
    [string] $PlanTo = '',

    # How many parts a run of every entry is cut into, the most a pull request gets, and how many
    # entries make one part of a pull request. The first two are decisions of 2026-10-16: sixteen
    # leaves four of the twenty jobs a free plan runs at once, and a pull request keeps room for
    # its own checks. Forty is a starting point, to be read again after the first runs.
    [int] $FullParts = 16,
    [int] $MostPartsOnAPullRequest = 8,
    [int] $EntriesPerPart = 40,

    # JUDGES ONLY WHAT CI RUNS. build.yml takes the integration project through the runs=anywhere
    # trait and nothing else, on purpose, so an entry naming an integration test without that
    # trait comes out NOT HERE rather than being judged on a machine its test was never meant for.
    [switch] $CiScope,

    # Writes the verdicts to this JSON file after every entry, so a part that is stopped half way
    # still says how far it got. tests/mutation/report.ps1 reads these.
    [string] $Json = '',

    # Skips the rebuild after the run. Only for a tree thrown away afterwards, which a CI runner
    # is - trap 1a above is about a tree somebody goes on using.
    [switch] $NoClosingBuild
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

# THE LIST LIVES IN entries.ps1 BESIDE THIS FILE, since 2026-10-16. Every object that script
# writes is an entry.
$mutations = @(& (Join-Path $PSScriptRoot 'entries.ps1'))

# WHAT A PULL REQUEST IS COMPARED BY, CHECKED BEFORE ANYTHING USES IT. Select-Reached finds an
# entry in the base list by its name, so two entries with one name would make one of them
# invisible to that comparison - and a re-anchoring written as a second entry under the old name
# is a mistake this registry has already made once. A missing field would make an entry that
# matches nothing and reads as STALE, which looks like moved code rather than a typo.
$problems = @()
foreach ($duplicate in ($mutations | Group-Object { $_.Name } | Where-Object Count -gt 1)) {
    $problems += "two entries are named '$($duplicate.Name)'"
}
foreach ($entry in $mutations) {
    if ($entry -isnot [hashtable]) { $problems += "entries.ps1 wrote something that is not an entry: $entry"; continue }
    foreach ($field in 'Name', 'File', 'From', 'Test') {
        if ([string]::IsNullOrEmpty([string] $entry[$field])) { $problems += "an entry has no $field - $($entry.Name)" }
    }
    if ($null -eq $entry['To']) { $problems += "an entry has no To - $($entry.Name)" }
}
if ($problems.Count) {
    $problems | ForEach-Object { Write-Output "entries.ps1: $_" }
    exit 1
}

# The canary. A mutation that cannot compile, which must be reported as BROKEN rather than as
# caught. If this one comes back "caught", the runner is counting build failures as evidence
# and every other line of its output is worthless.
$canary = @{
    Name = 'CANARY - deliberately does not compile'
    File = 'src\Bws.Core\SecondPass.cs'
    From = 'var files = Distinct(entries);'
    To = 'var files = Distinct(entries) this line cannot compile;'
    Test = 'SecondPassTests.One_question_per_distinct_file_however_many_are_asked_at_once'
    Expect = 'BROKEN'
}

# No "2>&1" and no Stop while this runs. In PowerShell 5.1 redirecting a native command's
# error stream wraps every line in an error record, which under $ErrorActionPreference = Stop
# ends the whole script the first time a test legitimately fails - which is the outcome this
# runner exists to produce. Cost half a run to find out.
function Invoke-Quiet([string] $Command, [string[]] $Arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        # BOTH CHANNELS, and until 2026-08-04 only the first. The test runner puts its per-test
        # failure marker on the error channel and its summary on the data channel, so a function
        # reading one of the two could see that a run failed and never see WHICH test did - which
        # is the difference between "the guard caught it" and "nothing ran under that name".
        $output = & $Command @Arguments 2>&1
        return [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output | Out-String) }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

function Test-Mutation($mutation, [string] $Root) {
    $path = Join-Path $Root $mutation.File
    $original = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

    # Line endings taken out of the comparison, because they are not what any entry here is
    # about. Found 2026-08-02: splitting a file with WriteAllLines turned it from LF to CRLF,
    # every multi-line anchor in this registry stopped matching it, and the run reported STALE
    # - which reads as "the code moved on" and was true of nothing but the newlines. The tree
    # is mixed either way, so matching the file byte for byte was never going to hold.
    #
    # The file is written back in the shape it was found in. Whatever this run does to the
    # tree, it must not be a line ending change nobody asked for.
    $wasCrlf = $original.Contains("`r`n")
    $flat = if ($wasCrlf) { $original.Replace("`r`n", "`n") } else { $original }
    $from = $mutation.From.Replace("`r`n", "`n")
    $to = $mutation.To.Replace("`r`n", "`n")

    if (-not $flat.Contains($from)) {
        # The code moved on and this entry now points at nothing. Loud, because an entry
        # matching nothing is an entry that proves nothing while looking like it did.
        return 'STALE'
    }

    $mutated = $flat.Replace($from, $to)

    if ($wasCrlf) { $mutated = $mutated.Replace("`n", "`r`n") }

    # No BOM and the original line endings: the tree has to differ in the one thing being
    # tested and nothing else.
    #
    # THE LINE ENDINGS ARE PUT BACK AND THE BOM IS NOT, and that is worth knowing rather than
    # fixing. Restoring a file that HAD a byte order mark returns it without one, so the file
    # comes back with a one-byte difference that git shows as a changed first line. Found
    # 2026-08-04, on CoreAssembly.cs, which was the only file in this repository carrying one -
    # so the run left the tree more consistent than it found it, and the surprise was the diff
    # rather than the change. Same family as the CRLF lesson above: the mutation was correct and
    # the rewriting was not neutral.
    [IO.File]::WriteAllText($path, $mutated, (New-Object Text.UTF8Encoding $false))

    try {
        # WARNINGS ARE NOT ERRORS FOR THE MUTATED TREE, AND ONLY FOR IT - since 2026-09-23.
        #
        # A mutation asks one question: does the named test go red when this behaviour is gone.
        # Whether the mutated code is TIDY is not part of it. Until that day the two were the same
        # thing, because warnings were errors here and a mutation almost never left anything
        # untidy. Then the hygiene package made an unused parameter, an unused private member and
        # an unnecessary using directive into build errors - and a mutation removes the only use
        # of something as often as not. MEASURED: the first full gate after that came back with 38
        # of 602 entries BROKEN, each proving nothing. Run again with this one switch and nothing
        # else changed, all 38 came back caught - the build errors were the whole of it.
        #
        # Rewriting 38 entries would have fixed 38, and the next strict rule would break the next
        # batch. So the mutated build tolerates warnings and the product's own build does not:
        # the rebuild after restore at the end of this script stays strict, and so does every
        # other build in this project. BROKEN still means what it meant - the mutation does not
        # compile at all.
        $build = Invoke-Quiet 'dotnet' @('build', (Join-Path $Root 'BetterWindowsServices.slnx'), '-c', 'Debug', '--nologo', '-v', 'q', '-p:TreatWarningsAsErrors=false')

        if ($build.Code -ne 0) {
            return 'BROKEN'
        }

        $run = Invoke-Quiet 'dotnet' @('test', (Join-Path $Root 'BetterWindowsServices.slnx'), '-c', 'Debug', '--no-build', '--nologo', '--filter', "FullyQualifiedName~$($mutation.Test)")

        # THE FAILURE MARKER IS ASKED FIRST, SINCE 2026-09-07, AND THE ORDER WAS A BUG THAT
        # REPORTED A WORKING GUARD AS MISSED. This run covers the whole solution, so a test NAME
        # that exists in two assemblies produces two summaries - and the assembly whose copy was
        # untouched prints "niepowodzenie: 0," beside the one that went red. The old order read
        # that first and answered MISSED, which is the worst verdict this script can get wrong: it
        # says a guard does not check what it claims, and the next session deletes or rewrites a
        # guard that was working.
        #
        # Measured rather than reasoned about. Two entries on 2026-09-07 came back MISSED, and the
        # same mutation applied by hand turned the named test red at once - which is the whole
        # difference between this script's answer and the truth.
        #
        # WHY THE MARKER IS SAFE TO TRUST FIRST. The filter names one test, so any [FAIL] in the
        # output is that test failing. It is xUnit's own marker and is English on any machine,
        # which is what the note below was written about.
        if ($run.Output -match '\[FAIL\]') {
            return 'caught'
        }

        # A filter matching no test also returns non-zero on some runners, which would read
        # as caught. The named test has to have run and failed, not merely be absent.
        if ($run.Output -match 'niepowodzenie:\s*0,' -or $run.Output -match 'failed:\s*0,') {
            return 'MISSED'
        }

        # No failure marker anywhere and no summary means nothing ran under this name.
        return 'STALE'

        # THIS CHECK WAS BROKEN FROM THE DAY IT WAS WRITTEN, twice over, and it was measured on
        # 2026-08-04 rather than noticed: an entry naming a test that does not exist came back
        # 'caught', printing "1 of 1 guards proved themselves" while proving nothing at all. That
        # is the exact failure this whole script exists to make impossible.
        #
        # Two causes, and the second is the one worth carrying to other scripts. The wording was
        # simply wrong - the runner says "Zaden test nie jest zgodny z podanym filtrem", not
        # "Nie znaleziono testow". And the pattern held a non-ASCII letter in a file with no byte
        # order mark, so Windows PowerShell read it as the machine code page and turned that one
        # letter into two: the string in memory could not have matched even the right sentence.
        #
        # The replacement is above, and getting to it took one wrong answer worth recording. The
        # first attempt looked for the filter being echoed back, on the grounds that the runner
        # only quotes a filter when nothing matched it. IT REPORTED STALE FOR A TEST THAT EXISTS,
        # because the run covers four test projects and the three not holding the named test say
        # so - so the echo is there on a perfectly good run as well.
        #
        # What is left is xUnit's own failure marker, which is English on any machine, and the
        # meaning is exactly what this function is for: the named test ran and it failed. No
        # marker and no summary means nothing ran under that name.
        #
        # Note also that dotnet test exits 0 when a filter matches nothing, so the exit code was
        # never going to help here either.
    }
    finally {
        [IO.File]::WriteAllText($path, $original, (New-Object Text.UTF8Encoding $false))
    }
}

# ---------------------------------------------------------------------------------------
# What a change reaches, which part a machine judges, and what CI runs - 2026-10-16
# ---------------------------------------------------------------------------------------

# Every test class and the files that declare it, with the project each file is in. A list rather
# than one file, because a class can be declared twice - a partial class, or the same name in two
# suites on purpose (cause 11 in entries.ps1). A declaration is a line that starts with modifiers
# and the keyword, so a comment saying "the class that" does not register a class named that.
function Get-ClassIndex([string] $Root) {
    $index = @{}
    $declaration = '(?m)^\s*(?:(?:public|internal|private|protected|sealed|static|partial|abstract|file)\s+)*(?:class|record)\s+([A-Za-z_]\w*)'

    foreach ($file in Get-ChildItem (Join-Path $Root 'tests') -Recurse -File -Filter '*.cs') {
        $relative = $file.FullName.Substring($Root.Length).TrimStart('\', '/') -replace '/', '\'
        if ($relative -match '\\(bin|obj)\\') { continue }

        $place = [pscustomobject]@{ File = $relative; Project = $relative.Split('\')[1] }

        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file.FullName), $declaration)) {
            $name = $match.Groups[1].Value
            if (-not $index.ContainsKey($name)) { $index[$name] = [Collections.Generic.List[object]]::new() }
            if (@($index[$name] | Where-Object { $_.File -eq $relative }).Count -eq 0) { $index[$name].Add($place) }
        }
    }

    return $index
}

# The class of the test an entry names, and the method if it names one. A test is written
# Class.Method, sometimes with the namespace in front, and one entry names a whole class - a filter
# on a class runs all of it. So the second segment from the right is asked first, then every other
# segment from the right, and the first one the index knows is the class.
function Find-TestClass([string] $Test, $Index) {
    $segments = $Test.Split('(')[0].Split('.')
    $order = @()
    if ($segments.Count -ge 2) { $order += $segments.Count - 2 }
    $order += @(($segments.Count - 1)..0 | Where-Object { $_ -notin $order })

    foreach ($at in $order) {
        if ($Index.ContainsKey($segments[$at])) {
            $method = if ($at -lt $segments.Count - 1) { $segments[$segments.Count - 1] } else { '' }
            return [pscustomobject]@{ Class = $segments[$at]; Method = $method; Places = @($Index[$segments[$at]]) }
        }
    }

    return [pscustomobject]@{ Class = ''; Method = ''; Places = @() }
}

# Whether the runs=anywhere trait reaches this class or this one method, read off the text. The
# trait stands on a whole class in some files and on single methods in others - both shapes were
# in the tree on 2026-10-16 - so asking "does the file carry it" would judge a method beside one
# that has it. What sits between the trait and the declaration may hold no brace and no semicolon,
# which is what keeps a trait on the previous method from reaching this one: that method has a body.
function Test-TraitReaches([string] $Text, [string] $Class, [string] $Method) {
    $trait = 'Trait\(\s*"runs"\s*,\s*"anywhere"\s*\)'
    if ($Text -match ('(?s)' + $trait + '[^{;]*?\b(?:class|record)\s+' + [regex]::Escape($Class) + '\b')) { return $true }
    if ($Method.Length -gt 0 -and $Text -match ('(?s)' + $trait + '[^{;]*?\b' + [regex]::Escape($Method) + '\s*\(')) { return $true }
    return $false
}

# NOT HERE under -CiScope: every place declaring the test's class is in the integration project and
# the trait reaches neither that class nor that method. A class that also lives in a suite CI runs
# is judged, because the filter runs that copy too. A class nobody declares is judged as well - the
# run then says STALE, which is the truth about an entry naming a test that does not exist.
function Test-NotHere($Entry, $Index, [string] $Root) {
    $found = Find-TestClass $Entry.Test $Index
    if ($found.Places.Count -eq 0) { return $false }

    foreach ($place in $found.Places) {
        if ($place.Project -ne 'Bws.Integration.Tests') { return $false }
        if (Test-TraitReaches ([IO.File]::ReadAllText((Join-Path $Root $place.File))) $found.Class $found.Method) { return $false }
    }

    return $true
}

# Whether an entry differs from the one under its name in the base. Line endings are taken out, for
# the reason Test-Mutation gives - a checkout with core.autocrlf hands the same entry over with
# other newlines, and that is not a change to what it breaks.
function Test-EntryDiffers($Now, $Then) {
    foreach ($field in 'File', 'From', 'To', 'Test') {
        if (([string] $Now[$field]).Replace("`r`n", "`n") -cne ([string] $Then[$field]).Replace("`r`n", "`n")) { return $true }
    }
    return $false
}

# THE ENTRIES A CHANGE REACHES, three ways, and the reason travels with each one:
#
#   its file     the change edits the file the entry mutates
#   its guard    the change edits a file declaring the class of the entry's test
#   itself       the entry is new, or its file, its two texts or its test differ from the base
#
# WHAT NONE OF THE THREE CAN SEE is a change in a THIRD file that stops a guard reaching its code -
# a helper both share, a base class, a style the test reads. That is what the weekly run of every
# entry is for, and it is why the pull request run reports rather than gates.
#
# The caller decides "everything" before this is asked: a changed runner, a base git cannot find,
# a base whose list cannot be read. Selecting nothing in those cases would read exactly like a
# change no guard covers.
function Select-Reached($Entries, $Previous, [string[]] $ChangedFiles, $Index) {
    $changedSet = @{}
    foreach ($one in $ChangedFiles) { if ($one) { $changedSet[($one -replace '/', '\').Trim()] = $true } }

    $before = @{}
    foreach ($one in $Previous) { $before[$one.Name] = $one }

    foreach ($entry in $Entries) {
        $why = ''

        if ($changedSet.ContainsKey(($entry.File -replace '/', '\').Trim())) {
            $why = 'its file'
        } elseif (@((Find-TestClass $entry.Test $Index).Places | Where-Object { $changedSet.ContainsKey($_.File) }).Count -gt 0) {
            $why = 'its guard'
        } elseif (-not $before.ContainsKey($entry.Name)) {
            $why = 'a new entry'
        } elseif (Test-EntryDiffers $entry $before[$entry.Name]) {
            $why = 'a changed entry'
        }

        if ($why) { [pscustomobject]@{ Entry = $entry; Why = $why } }
    }
}

# How many parts: a run of every entry gets the full count, a pull request one part per forty
# entries and never more than its ceiling, and never more parts than entries. None for none - an
# empty matrix is an error on GitHub, so the workflow asks this number before it starts one.
function Get-PartCount([int] $Count, [bool] $PullRequest) {
    if ($Count -le 0) { return 0 }
    $parts = if ($PullRequest) { [Math]::Min($MostPartsOnAPullRequest, [int] [Math]::Ceiling($Count / $EntriesPerPart)) } else { $FullParts }
    return [Math]::Max(1, [Math]::Min($parts, $Count))
}

# The matrix GitHub expands, as JSON. Spelled out for zero because 0..-1 in PowerShell is the two
# numbers 0 and -1, not an empty range, and the workflow would start a part that does not exist.
function Get-ShardList([int] $Parts) {
    if ($Parts -le 0) { return '[]' }
    return '[' + ((0..($Parts - 1)) -join ',') + ']'
}

# Part i of k: entry n of the selection goes to part n mod k. The selection keeps the order of
# entries.ps1, so every machine computing it from the same commit gets the same share.
function Select-Part($All, [int] $Part, [int] $Of) {
    $list = @($All)
    for ($n = 0; $n -lt $list.Count; $n++) {
        if ($n % $Of -eq $Part) { $list[$n] }
    }
}

$selected = $mutations

if ($Only.Length -gt 0) {
    # SEVERAL FRAGMENTS, SEPARATED BY |, since 2026-09-24. A package that adds twenty-nine entries
    # and re-anchors eleven has forty names with no fragment in common, and the choice was forty
    # runs (a canary and a closing rebuild each) or -Touched over 155 entries. An entry is taken
    # when ANY fragment is in its name. A single fragment behaves exactly as it always did.
    #
    # PLAIN TEXT UNDER -like, NOT A REGULAR EXPRESSION - paid 2026-09-29: 'kept files: a (change|new
    # family)' was cut at the bar into two fragments holding a bracket each, both matched nothing, and
    # the run reported 21 of 21 without the two entries it was asked for. Spell every name out.
    $fragments = @($Only -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 })
    $selected = @($mutations | Where-Object {
        $name = $_.Name
        @($fragments | Where-Object { $name -like "*$_*" }).Count -gt 0
    })
}

# ---------------------------------------------------------------------------------------
# -Touched: the entries anchored in files this change moved
# ---------------------------------------------------------------------------------------

if ($Touched) {
    Push-Location $Root
    try {
        # Three sources, because one of them alone is a lie in a way that is easy to miss.
        # `git diff --name-only <ref>` misses files nobody has added yet, and a new file is
        # exactly where a new guard tends to live. `--cached` misses the unstaged half.
        $changed = @()
        $changed += @(git diff --name-only $Since 2>$null)
        $changed += @(git diff --name-only --cached $Since 2>$null)
        $changed += @(git ls-files --others --exclude-standard 2>$null)
    }
    finally {
        Pop-Location
    }

    # Git speaks forward slashes and the registry is written with backslashes. Compared after
    # normalising both, rather than by hoping - a comparison that never matches would select
    # nothing and print a clean run over an empty set.
    $changedSet = @{}
    foreach ($one in ($changed | Where-Object { $_ })) {
        $changedSet[($one -replace '/', '\').Trim()] = $true
    }

    $touchedProduct = @($changedSet.Keys | Where-Object { $_ -like 'src\*' -or $_ -like 'tests\*' })

    $before = @($selected).Count

    $selected = @($selected | Where-Object {
        # An entry with no File finds its anchor at run time - the size ratchet does this,
        # because the only mutation that can prove it is a line added to whichever file is
        # longest today. It cannot be matched against a path, so it rides along whenever the
        # product or its tests moved at all.
        if (-not $_.File) { return $touchedProduct.Count -gt 0 }

        return $changedSet.ContainsKey(($_.File -replace '/', '\').Trim())
    })

    Write-Output ''
    Write-Output ("-Touched against {0}: {1} changed file(s), {2} of {3} entries selected." -f
        $Since, $changedSet.Count, @($selected).Count, $before)

    # SILENCE IS FORBIDDEN. The number of entries NOT run is the whole
    # risk of this switch, so it is printed rather than left to be worked out from two numbers.
    Write-Output ("{0} entries were NOT run. This is the loop, not the gate - the full registry" -f ($before - @($selected).Count))
    Write-Output 'is still the only thing that proves a change did not disarm a guard elsewhere.'

    if (@($selected).Count -eq 0) {
        Write-Output ''

        if ($changedSet.Count -eq 0) {
            Write-Output 'Nothing has changed against that reference, so there was nothing to prove.'
        } else {
            Write-Output 'NONE of the changed files carries an entry in this registry. That is worth'
            Write-Output 'reading rather than skipping: either the change needs no guard, or the guard'
            Write-Output 'it needs has not been written. Files changed:'
            foreach ($one in ($changedSet.Keys | Sort-Object)) { Write-Output ("  {0}" -f $one) }
        }

        # Exit 3 rather than 0, and rather than 1. Zero would read as "every guard proved
        # itself" over an empty set, which is the shape of silent wrongness this whole file
        # exists against. One would read as a red guard. Three says "measured nothing", and
        # check.ps1 -Loop knows to pass that through with a note.
        exit 3
    }
}

# ---------------------------------------------------------------------------------------
# -Base, -PlanTo, -Shard and -CiScope: how CI cuts the work - 2026-10-16
# ---------------------------------------------------------------------------------------

# The files that make every verdict, so a change to any of them selects every entry. The workflow
# is one of them because it decides how the runner is called - a pull request changing it with no
# entry selected would never run the thing it changed.
$runnerFiles = @('tests/mutation/mutate.ps1', 'tests/mutation/report.ps1', '.github/workflows/mutation.yml')

# Always a list from here on, whatever the blocks above did. A bare hashtable - one entry matched
# without @() around it - answers .Count with the number of its KEYS, five for an entry, not one.
$selected = @($selected)

$classIndex = $null
if ($Base.Length -gt 0 -or $CiScope) { $classIndex = Get-ClassIndex $Root }

$selectedWhy = @{}

if ($Base.Length -gt 0) {
    $everything = ''
    $previous = @()
    $changedFiles = @()

    Push-Location $Root
    try {
        # Three dots: what the pull request changed against the point it left the base, which is
        # what a reviewer sees, and not what the base gained since.
        $changedFiles = @(git diff --name-only "$Base...HEAD" 2>$null)
        if ($LASTEXITCODE -ne 0) { $everything = "git could not compare with $Base" }

        if (-not $everything -and @($changedFiles | Where-Object { $_ -in $runnerFiles }).Count -gt 0) {
            $everything = 'the runner, the report or the workflow changed'
        }

        if (-not $everything) {
            $baseText = @(git show "${Base}:tests/mutation/entries.ps1" 2>$null)
            if ($LASTEXITCODE -ne 0) {
                $everything = 'the base has no tests/mutation/entries.ps1'
            } else {
                # The base list is RUN, not parsed - the same way this script reads its own list, so the
                # two cannot disagree about what an entry is. It comes from a commit of this repository.
                $baseFile = Join-Path ([IO.Path]::GetTempPath()) ("bws-mutation-base-{0}.ps1" -f $PID)
                [IO.File]::WriteAllText($baseFile, ($baseText -join "`n") + "`n", (New-Object Text.UTF8Encoding $false))
                try {
                    $previous = @(& $baseFile)
                } catch {
                    $everything = "the list in the base could not be read: $($_.Exception.Message)"
                } finally {
                    [IO.File]::Delete($baseFile)
                }
            }
        }
    }
    finally {
        Pop-Location
    }

    $before = $selected.Count

    if ($everything) {
        $selected = @($selected)
        foreach ($entry in $selected) { $selectedWhy[$entry.Name] = 'everything' }
        Write-Output ("-Base {0}: every entry, because {1}." -f $Base, $everything)
    } else {
        $reached = @(Select-Reached $selected $previous $changedFiles $classIndex)
        $selected = @($reached | ForEach-Object { $_.Entry })
        foreach ($one in $reached) { $selectedWhy[$one.Entry.Name] = $one.Why }
        Write-Output ("-Base {0}: {1} changed file(s), {2} of {3} entries reached." -f $Base, $changedFiles.Count, $selected.Count, $before)
        foreach ($group in ($reached | Group-Object Why | Sort-Object Name)) {
            Write-Output ("  {0,5}  through {1}" -f $group.Count, $group.Name)
        }
    }

    # Silence is forbidden here too: what was not run is said, not left to subtraction.
    Write-Output ("{0} entries were NOT selected. The weekly run of every entry is what judges them." -f ($before - $selected.Count))
}

if ($PlanTo.Length -gt 0) {
    $parts = Get-PartCount $selected.Count ($Base.Length -gt 0)
    $lines = @("entries=$($selected.Count)", "parts=$parts", "shards=$(Get-ShardList $parts)")

    # Appended with a bare newline. GitHub reads this file line by line, and Add-Content would
    # choose the line ending for us - it has written CRLF into an LF file in this project before.
    [IO.File]::AppendAllText($PlanTo, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))

    Write-Output ''
    $lines | ForEach-Object { Write-Output "plan: $_" }
    exit 0
}

$partNumber = 0
$partCount = 1
$selectedTotal = @($selected).Count

if ($Shard.Length -gt 0) {
    if ($Shard -notmatch '^(\d+)/(\d+)$' -or [int] $Matches[2] -lt 1 -or [int] $Matches[1] -ge [int] $Matches[2]) {
        throw "-Shard takes i/k with i counted from zero and below k, and was given '$Shard'"
    }

    $partNumber = [int] $Matches[1]
    $partCount = [int] $Matches[2]
    $selected = @(Select-Part $selected $partNumber $partCount)

    Write-Output ("part {0} of {1}: {2} of the {3} selected entries." -f $partNumber, $partCount, $selected.Count, $selectedTotal)
}

$notHere = @()
if ($CiScope) {
    $notHere = @($selected | Where-Object { Test-NotHere $_ $classIndex $Root })
    $selected = @($selected | Where-Object { $_ -notin $notHere })

    if ($notHere.Count) {
        Write-Output ("{0} entries name a test CI does not run and are NOT HERE - judged on a developer machine." -f $notHere.Count)
    }
}

# What -Json writes, kept up to date after every entry. A part that is stopped - a time limit, a
# cancelled run, a machine that went away - leaves a file with finished = false, and report.ps1
# reads that as a part that did not finish rather than as a smaller run.
$partState = [ordered]@{
    shard = $partNumber
    parts = $partCount
    selected = $selectedTotal
    entries = $selected.Count + $notHere.Count
    started = [DateTime]::UtcNow.ToString('o')
    seconds = 0
    canary = ''
    built = $true
    baselineRed = @()
    finished = $false
    results = [Collections.Generic.List[object]]::new()
}
$partClock = [Diagnostics.Stopwatch]::StartNew()
$partFile = if ($Json.Length -gt 0) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Json) } else { '' }

function Write-PartState {
    if (-not $partFile) { return }
    $partState.seconds = [Math]::Round($partClock.Elapsed.TotalSeconds, 1)
    [IO.File]::WriteAllText($partFile, ($partState | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
}

foreach ($entry in $notHere) {
    $partState.results.Add([ordered]@{ verdict = 'NOT HERE'; name = $entry.Name; test = $entry.Test; file = $entry.File; why = $selectedWhy[$entry.Name]; seconds = 0 })
}

# ---------------------------------------------------------------------------------------
# The baseline: every named test once on the UNMUTATED tree - backlog 477, 2026-09-29
# ---------------------------------------------------------------------------------------

# The tests xUnit marks [FAIL] in a run's output. The marker is xUnit's own and English on any
# machine, the same one Test-Mutation trusts, and the name is everything between the timestamp and
# the marker - a theory's name carries its arguments, and those can hold spaces.
function Get-FailedTests([string] $Output) {
    $names = [regex]::Matches($Output, '\[xUnit\.net [^\]]*\]\s+(.+?)\s+\[FAIL\]') |
        ForEach-Object { $_.Groups[1].Value.Trim() }

    # No leading comma: callers wrap this in @(), and the two together would hand back an array
    # holding the array - the self test below caught exactly that on the day it was written.
    return [string[]] @($names | Sort-Object -Unique)
}

# Whether the test an entry names is among those red before any mutation. By the same substring
# rule the mutant's own run filters with (FullyQualifiedName~), so the two cannot disagree about
# which tests one name stands for - a name matching a theory and a plain test is red when either is.
function Test-BaselineRed([string] $Test, [string[]] $Red) {
    return @($Red | Where-Object { $_.Contains($Test) }).Count -gt 0
}

# One build of the clean tree, then the named tests forty to a run: a filter is one argument, and a
# full registry names close to two thousand tests. Built exactly as a mutant is built, so a test red
# here is red for the same compiler and the same switches.
function Invoke-Baseline($Entries, [string] $Root) {
    $solution = Join-Path $Root 'BetterWindowsServices.slnx'
    $build = Invoke-Quiet 'dotnet' @('build', $solution, '-c', 'Debug', '--nologo', '-v', 'q', '-p:TreatWarningsAsErrors=false')

    if ($build.Code -ne 0) {
        return [pscustomobject]@{ Built = $false; Red = [string[]] @(); Ran = 0 }
    }

    $names = [string[]] @($Entries | ForEach-Object { $_.Test } | Sort-Object -Unique)
    $red = [Collections.Generic.List[string]]::new()

    for ($at = 0; $at -lt $names.Count; $at += 40) {
        $batch = $names[$at..([Math]::Min($at + 39, $names.Count - 1))]
        $filter = ($batch | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
        $run = Invoke-Quiet 'dotnet' @('test', $solution, '-c', 'Debug', '--no-build', '--nologo', '--filter', $filter)

        foreach ($one in @(Get-FailedTests $run.Output)) { $red.Add($one) }
    }

    return [pscustomobject]@{ Built = $true; Red = [string[]] @($red | Sort-Object -Unique); Ran = $names.Count }
}

if ($SelfTest) {
    # An EXTERNAL canary in the sense of tools/selftest.ps1: an xUnit output with two red tests in
    # it, one a theory whose arguments hold a space, written the way the runner writes them.
    $planted = @(
        '  [xUnit.net 00:00:00.43]     Bws.Core.Tests.SnapshotGoldenTests.The_file_is_written_byte_for_byte_as_the_kept_copy [FAIL]'
        '  [xUnit.net 00:00:00.51]     Bws.Integration.Tests.SnapshotContractTests.A_snapshot_saved_with_a_byte_order_mark_still_reads(encoding: "utf 16") [FAIL]'
        '    Niepowodzenie Bws.Core.Tests.SnapshotGoldenTests.The_file_is_written_byte_for_byte_as_the_kept_copy [48 ms]'
        '  Niepowodzenie!    - niepowodzenie:     2, powodzenie:     9, pominieto:     0, lacznie:    11'
    ) -join "`n"

    $red = @(Get-FailedTests $planted)
    $problems = @()

    if ($red.Count -ne 2) { $problems += "read $($red.Count) red tests out of the planted output, expected 2: $($red -join ' / ')" }
    if (-not (Test-BaselineRed 'SnapshotGoldenTests.The_file_is_written_byte_for_byte_as_the_kept_copy' $red)) { $problems += 'a planted red test was not BASELINE RED' }
    if (-not (Test-BaselineRed 'SnapshotContractTests.A_snapshot_saved_with_a_byte_order_mark_still_reads' $red)) { $problems += 'a planted red theory was not BASELINE RED' }
    if (Test-BaselineRed 'SnapshotTests.The_file_ends_with_a_newline' $red) { $problems += 'a test that was not red came out BASELINE RED' }
    if (@(Get-FailedTests 'Powodzenie! - niepowodzenie: 0').Count -ne 0) { $problems += 'a green run produced a red test' }

    # THE SELECTION A PULL REQUEST GETS, 2026-10-16: one entry per way of being reached, one entry
    # nothing reaches, and an entry whose only difference from the base is its line endings - which
    # must NOT count, or every checkout with core.autocrlf would select it. A synthetic index, so
    # nothing here reads the tree.
    $index = @{
        'AlphaTests' = @([pscustomobject]@{ File = 'tests\Bws.Core.Tests\AlphaTests.cs'; Project = 'Bws.Core.Tests' })
        'BetaContractTests' = @([pscustomobject]@{ File = 'tests\Bws.Integration.Tests\BetaContractTests.cs'; Project = 'Bws.Integration.Tests' })
    }
    $now = @(
        @{ Name = 'reached through its file'; File = 'src\Bws.Core\A.cs'; From = 'a'; To = 'b'; Test = 'AlphaTests.One' }
        @{ Name = 'reached through its guard'; File = 'src\Bws.Core\B.cs'; From = 'a'; To = 'b'; Test = 'Bws.Integration.Tests.BetaContractTests.Two' }
        @{ Name = 'new'; File = 'src\Bws.Core\C.cs'; From = 'a'; To = 'b'; Test = 'AlphaTests.Three' }
        @{ Name = 'changed'; File = 'src\Bws.Core\D.cs'; From = 'a'; To = 'c'; Test = 'AlphaTests.Four' }
        @{ Name = 'untouched'; File = 'src\Bws.Core\E.cs'; From = "x`r`ny"; To = 'b'; Test = 'AlphaTests.Five' }
    )
    $then = @(
        @{ Name = 'reached through its file'; File = 'src\Bws.Core\A.cs'; From = 'a'; To = 'b'; Test = 'AlphaTests.One' }
        @{ Name = 'reached through its guard'; File = 'src\Bws.Core\B.cs'; From = 'a'; To = 'b'; Test = 'Bws.Integration.Tests.BetaContractTests.Two' }
        @{ Name = 'changed'; File = 'src\Bws.Core\D.cs'; From = 'a'; To = 'b'; Test = 'AlphaTests.Four' }
        @{ Name = 'untouched'; File = 'src\Bws.Core\E.cs'; From = "x`ny"; To = 'b'; Test = 'AlphaTests.Five' }
    )
    $reached = @(Select-Reached $now $then @('src/Bws.Core/A.cs', 'tests/Bws.Integration.Tests/BetaContractTests.cs') $index)
    $said = ($reached | ForEach-Object { "$($_.Entry.Name)=$($_.Why)" }) -join ' / '
    $expected = 'reached through its file=its file / reached through its guard=its guard / new=a new entry / changed=a changed entry'
    if ($said -ne $expected) { $problems += "the selection said '$said', expected '$expected'" }
    if (@(Select-Reached $now $now @() $index).Count -ne 0) { $problems += 'an unchanged list with no changed file selected something' }

    # A whole class named, and a namespace in front.
    if ((Find-TestClass 'BetaContractTests' $index).Class -ne 'BetaContractTests') { $problems += 'a test naming a whole class did not find it' }
    if ((Find-TestClass 'Bws.Integration.Tests.BetaContractTests.Two' $index).Method -ne 'Two') { $problems += 'a namespaced test lost its method' }

    # The trait on a class, on one method, on the method next door, and nowhere.
    $onClass = "[Trait(`"runs`", `"anywhere`")]`npublic sealed class BetaContractTests`n{`n    [Fact]`n    public void Two() { }`n}"
    $onOther = "public sealed class BetaContractTests`n{`n    [Fact]`n    [Trait(`"runs`", `"anywhere`")]`n    public void One() { }`n`n    [Fact]`n    public void Two() { }`n}"
    $onThis = "public sealed class BetaContractTests`n{`n    [Fact]`n    [Trait(`"runs`", `"anywhere`")]`n    public void Two() { }`n}"
    if (-not (Test-TraitReaches $onClass 'BetaContractTests' 'Two')) { $problems += 'a trait on the class did not reach its method' }
    if (-not (Test-TraitReaches $onThis 'BetaContractTests' 'Two')) { $problems += 'a trait on the method did not reach it' }
    if (Test-TraitReaches $onOther 'BetaContractTests' 'Two') { $problems += 'a trait on the method next door reached this one' }
    if (Test-TraitReaches 'public sealed class BetaContractTests { }' 'BetaContractTests' 'Two') { $problems += 'a class with no trait was taken as running anywhere' }

    # The cut into parts: nothing lost, nothing twice, and the counts the workflow is given.
    $cut = @(0..2 | ForEach-Object { @(Select-Part (1..7) $_ 3) })
    if ((($cut | Sort-Object) -join ',') -ne '1,2,3,4,5,6,7') { $problems += "three parts of seven gave $($cut -join ',')" }
    if (@(Select-Part (1..7) 0 3).Count -ne 3) { $problems += 'part 0 of 3 over seven entries did not hold three' }
    $counts = @((Get-PartCount 0 $true), (Get-PartCount 1 $true), (Get-PartCount 41 $true), (Get-PartCount 1228 $true), (Get-PartCount 1228 $false), (Get-PartCount 10 $false)) -join ','
    if ($counts -ne '0,1,2,8,16,10') { $problems += "the part counts were $counts, expected 0,1,2,8,16,10" }
    if ((Get-ShardList 0) -ne '[]' -or (Get-ShardList 3) -ne '[0,1,2]') { $problems += 'the matrix for zero or three parts came out wrong' }

    if ($problems.Count) {
        $problems | ForEach-Object { Write-Output "self test FAILED: $_" }
        exit 1
    }

    Write-Output 'self test passed: a planted red test and a red theory come out BASELINE RED, a green one does not'
    Write-Output 'self test passed: the selection, the trait, the cut into parts and the part counts answer as known in advance'
    exit 0
}

$results = @()

# A part whose every entry is NOT HERE has nothing to build. Said and finished, rather than a canary
# and a baseline build spent on no question.
if ($selected.Count -eq 0) {
    $partState.finished = $true
    Write-PartState
    Write-Output ("nothing to judge here: {0} selected, {1} of them NOT HERE." -f $partState.entries, $notHere.Count)

    # Three when nothing was selected at all - an -Only that matched no name is a typo, and zero
    # would read as every guard proving itself. Zero when everything selected was NOT HERE.
    if ($partState.entries -eq 0) { exit 3 } else { exit 0 }
}

if (-not $NoCanary) {
    $verdict = Test-Mutation $canary $Root
    $ok = $verdict -eq 'BROKEN'
    $partState.canary = $verdict

    Write-Output ("{0,-8} {1}" -f $verdict, $canary.Name)

    if (-not $ok) {
        $partState.finished = $true
        Write-PartState
        Write-Output ''
        Write-Output 'The canary did not come back BROKEN. This runner is not telling build failures'
        Write-Output 'apart from caught mutations, so nothing below can be believed. Stopping.'
        exit 1
    }
}

Write-PartState

$baseline = Invoke-Baseline $selected $Root

if (-not $baseline.Built) {
    $partState.built = $false
    $partState.finished = $true
    Write-PartState
    Write-Output ''
    Write-Output 'THE UNMUTATED TREE DOES NOT BUILD, so no test here can be red because of a mutation.'
    Write-Output 'Build it and read the error before running this again. Stopping.'
    exit 1
}

$partState.baselineRed = @($baseline.Red)
Write-PartState

Write-Output ("baseline {0} named test(s) run on the unmutated tree, {1} red before any mutation" -f $baseline.Ran, $baseline.Red.Count)

foreach ($one in $baseline.Red) { Write-Output ("         red already: {0}" -f $one) }

foreach ($mutation in $selected) {
    $entryClock = [Diagnostics.Stopwatch]::StartNew()
    $verdict = if (Test-BaselineRed $mutation.Test $baseline.Red) { 'BASELINE RED' } else { Test-Mutation $mutation $Root }
    $results += [pscustomobject]@{ Verdict = $verdict; Name = $mutation.Name; Test = $mutation.Test }

    $partState.results.Add([ordered]@{
        verdict = $verdict; name = $mutation.Name; test = $mutation.Test; file = $mutation.File
        why = $selectedWhy[$mutation.Name]; seconds = [Math]::Round($entryClock.Elapsed.TotalSeconds, 1)
    })
    Write-PartState

    Write-Output ("{0,-8} {1}" -f $verdict, $mutation.Name)
}

$partState.finished = $true
Write-PartState

# THE TREE IS RESTORED AND THE BINARIES ARE NOT, so they are rebuilt here before anybody is
# told the run finished.
#
# Paid for on 2026-08-02, and it cost twenty minutes of hunting a regression that did not
# exist. The last entry in this registry mutates MainViewModel so that a full reading rebuilds
# every row - and the last thing this script did was compile that. `dotnet test --no-build`
# afterwards ran the mutated build against restored sources, reported a real-looking failure in
# a guard about row identity, and did it four times running because nothing about it was
# random.
#
# This project already knew the shape from the other direction: after a failed build,
# --no-build tests the previous binaries and passes. Same trap, opposite sign, and the tree
# looking clean in git is exactly what makes both of them convincing.
if ($NoClosingBuild) {
    Write-Output ''
    Write-Output 'NOT rebuilding at the end (-NoClosingBuild): the binaries are still the last mutation,'
    Write-Output 'which is harmless only because this tree is thrown away.'
} else {
    Write-Output ''
    Write-Output 'restoring the build, because the binaries are still the last mutation'

    $rebuild = Invoke-Quiet 'dotnet' @('build', (Join-Path $Root 'BetterWindowsServices.slnx'), '-c', 'Debug', '--nologo', '-v', 'q')

    if ($rebuild.Code -ne 0) {
        Write-Output ''
        Write-Output 'THE REBUILD FAILED. The sources are restored but the binaries are not - anything'
        Write-Output 'run with --no-build from here is testing a mutation. Build before believing it.'
    }
}

$caught = @($results | Where-Object { $_.Verdict -eq 'caught' }).Count

Write-Output ''
Write-Output ("{0} of {1} guards proved themselves." -f $caught, $results.Count)

foreach ($bad in $results | Where-Object { $_.Verdict -ne 'caught' }) {
    Write-Output ("  {0}: {1}  ->  {2}" -f $bad.Verdict, $bad.Name, $bad.Test)
}

foreach ($entry in $notHere) {
    Write-Output ("  NOT HERE: {0}  ->  {1}" -f $entry.Name, $entry.Test)
}

Write-Output ''
Write-Output 'A MISSED entry means the guard does not check what its name says. Fix the SENTENCE'
Write-Output 'or the guard, never the mutation. A STALE entry means the code moved and this'
Write-Output 'entry now matches nothing, which proves nothing while looking like it did.'
Write-Output 'A BASELINE RED entry names a test that is red with no mutation at all - make it green'
Write-Output 'first, or say why it is red in this session. Its caught would have meant nothing.'

if ($caught -ne $results.Count) { exit 1 } else { exit 0 }
