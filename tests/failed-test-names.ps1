# THE NAMES OF THE TESTS A RED `dotnet test` RUN FAILED ON, IN ONE PLACE.
#
# Dot-sourced by tests\coverage-gate.ps1 and by tools\coverage-probe\uncovered.ps1. It is a file
# of its own rather than a function copied into each, for the reason both of those files already
# argue in their own headers: two places carrying one rule is a failure this project keeps paying
# for. tools\backlog-words.ps1 is the same shape, created after two copies of a five word list
# drifted for a day and made two tools answer one question differently.
#
# WHY IT EXISTS AT ALL, and it is a measured loss rather than a tidiness argument. Until
# 2026-09-01 both callers sent the runner's output to Out-Null and then said "did not pass" and
# nothing else, so on a red suite the evidence was destroyed in the same breath as the failure was
# reported. It cost this project twice in one day: two red tests from a full run were never
# written down and did not repeat, and a third red step had to be re-run to find out it was a
# clipboard test. Backlog 267, and rule 15 of CLAUDE.md is the rule it broke.
#
# KEYED ON [FAIL], BECAUSE IT IS THE ONE TOKEN IN THAT OUTPUT THAT IS NOT TRANSLATED. Measured
# rather than assumed: in tools\check-logs\20260826-205804, on a machine whose runner answers in
# Polish, the xUnit reporter printed "... [FAIL]" in English on the very lines where the VSTest
# runner one line below printed its own localised word for a failure. A distiller keyed on those
# labels would find everything on an English machine and nothing on this one - rule 3 of
# CLAUDE.md, one layer down from service names.
#
# THIS FILE IS KEPT IN PURE ASCII ON PURPOSE. It has no byte order mark, and the guard that would
# catch that combination - tools\tools-check.ps1 - scans tools\ only, so nothing in this
# repository is watching it. Backlog 268 carries the gap in the guard.
#
# WHAT IT IS NOT: a replacement for the output. Both callers print or keep the raw text whole, and
# these names are a signpost on top of it. Filtering is how the assertion message was lost the
# first time this project went red without a trace, so a marker that ever stops matching has to
# cost a convenience and never the evidence. The self test in tests\coverage-gate.ps1 -SelfTest
# proves this function on a real specimen, and proves it can fail.
function Get-FailedTestNames([string[]] $Lines) {
    return @($Lines |
        Where-Object { $_ -match '\[FAIL\]\s*$' } |
        ForEach-Object { (($_ -replace '^\s*\[xUnit\.net[^\]]*\]\s*', '') -replace '\s*\[FAIL\]\s*$', '').Trim() } |
        Where-Object { $_ } |
        Select-Object -Unique)
}
