#!/usr/bin/env bash
# Runs the same gates as CI (.github/workflows/ci.yml), in the same order, on this machine: G1 (restore with
# package audit, build, frontend audit and build), G2 (unit tests), G3 (API tests on .NET 10),
# G4 and G5 (gate scripts), G6 (gitleaks on the files and on the commits since origin/master).
# Green here means green in CI. Stops at the first failing gate. Needs: .NET 10 SDK, Node 20, Docker,
# Python 3. On Windows run it from Git Bash, or through scripts/verify.ps1.
# Usage: scripts/verify.sh            all gates
#        scripts/verify.sh --no-frontend   skip npm ci, audit and build (G1 frontend) when only backend changed
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
FRONTEND=1
[ "${1:-}" = "--no-frontend" ] && FRONTEND=0

if command -v py >/dev/null 2>&1; then PYTHON=(py -3); else PYTHON=(python3); fi

step() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }
fail() { printf '\n\033[31mFAILED: %s\033[0m\n' "$1"; exit 1; }

step "Prerequisites"
dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 10\.' \
  || fail ".NET 10 runtime missing: install the .NET 10 SDK (Windows: winget install Microsoft.DotNet.SDK.10)."
docker info >/dev/null 2>&1 || fail "Docker is not running: the API tests and gitleaks need it."

step "G1 restore and package audit"
dotnet restore backend/backend.sln || fail "G1 restore (a high or critical vulnerable package fails here)"
dotnet restore e2e/HospitalManagement.E2E || fail "G1 restore of the E2E project (package audit)"

step "G1 build"
dotnet build backend/backend.sln --no-restore -c Release || fail "G1 build"

if [ "$FRONTEND" = 1 ]; then
  step "G1 frontend: install, audit, build"
  (cd frontend-web && npm ci && npm audit --omit=dev --audit-level=high && npm run build) || fail "G1 frontend"
  (cd frontend-web && npm audit --audit-level=high >/dev/null) \
    || printf '\033[33mWarning: build-tool (dev) dependencies have high or critical vulnerabilities (not gating).\033[0m\n'
fi

step "G2 unit tests"
dotnet test backend/tests/HospitalManagement.UnitTests --no-build -c Release || fail "G2 unit tests"

step "G3 API tests (.NET 10)"
dotnet test backend/tests/HospitalManagement.ApiTests --no-build -c Release || fail "G3 API tests"

step "G4 and G5 gate scripts"
"${PYTHON[@]}" scripts/check_test_gates.py || fail "G4/G5"
"${PYTHON[@]}" -I scripts/check_doc_references.py || fail "G4 (c) citations"

step "G6 secret scan"
# Files: what CI checks out plus what you would commit (tracked and untracked, not git-ignored), copied to a
# temporary folder, so git-ignored local files such as .env, node_modules and bin/ are not scanned.
FILES="$(mktemp -d)"
trap 'rm -rf "$FILES"' EXIT
git ls-files -z -co --exclude-standard | tar --null -T - -cf - 2>/dev/null | tar -xf - -C "$FILES"
REPO="$ROOT"; SCAN="$FILES"
command -v cygpath >/dev/null 2>&1 && { REPO="$(cygpath -w "$ROOT")"; SCAN="$(cygpath -w "$FILES")"; }
BASE="$(git merge-base origin/master HEAD)"
export MSYS_NO_PATHCONV=1
docker run --rm -v "$SCAN:/repo:ro" zricethezav/gitleaks:v8.21.2 \
  detect --no-git --source /repo --redact --no-banner || fail "G6 gitleaks, files"
docker run --rm -v "$REPO:/repo:ro" \
  -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0='*' \
  zricethezav/gitleaks:v8.21.2 \
  detect --source /repo --log-opts="$BASE..HEAD" --redact --no-banner || fail "G6 gitleaks, commits since origin/master"

printf '\n\033[32mAll local gates passed (G1-G6). G7 (E2E) and G8 (review) run on the pull request.\033[0m\n'
