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
# v2.7.0 is the newest release on 2026-10-26, published on 2026-10-12, so it is past the cooldown
# above. The first pin was v2.6.0 because v2.7.0 was two days old on 2026-10-14 and the cooldown
# applied to that pin as well. The digest is the multi-architecture index, read from ghcr.io on
# 2026-10-26, and the same read on v2.6.0 returned the digest that was pinned here before, which
# is how the reading was checked. The image's entry point is the scanner itself.
FROM ghcr.io/google/osv-scanner:v2.7.0@sha256:4f3b7e2c64b9d38a5febc5a7e4a3dfef1d1362f726dea7a2ef4762b46469fcd0
