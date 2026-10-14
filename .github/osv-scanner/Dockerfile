# The OSV-Scanner that .github/workflows/osv-scanner.yml runs, named by its bytes.
#
# WHY A FILE WITH ONE LINE IN IT, and not the scanner's own GitHub Action. That action is pinned
# to a commit here like every other, and its action.yml then runs
# `docker://ghcr.io/google/osv-scanner-action:<tag>` - a TAG in a registry, which its owners can
# point at different bytes at any time. Pinning the action would have pinned nothing that runs.
# Read on the action's v2.6.0 on 2026-10-14. WorkflowGuards explains why a tag is not a pin.
#
# WHY HERE AND NOT IN THE WORKFLOW. A digest written into a `run:` line is watched by nothing -
# the lesson .github/requirements-semgrep.txt was written for. Dependabot reads Dockerfiles, so
# the `docker` entry in .github/dependabot.yml proposes the next release of the scanner here,
# tag and digest together, after the same seven days of cooldown as everything else.
#
# v2.6.0 rather than v2.7.0, which was the newest release on 2026-10-14 but two days old - the
# cooldown above applied to the first pin as well. The digest is the multi-architecture index,
# read from ghcr.io the same day. The image's entry point is the scanner itself.
FROM ghcr.io/google/osv-scanner:v2.6.0@sha256:afd838850ac1a0fcc15ff4a041dc9ba11123c3f0d2666217a5f0fcf9222b55fa
