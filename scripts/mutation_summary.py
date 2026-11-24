#!/usr/bin/env python3
"""Mutation summary for a pull request (docs/testing/TEST_STRATEGY.md §11).

Reads a Stryker.NET JSON report and prints Markdown for the job summary: the score of every mutated file and
every mutant no test noticed (Survived, NoCoverage). Mutants on lines this pull request changed are marked, because
they are the gaps the change itself brings; the others were already in the file.

With --fail-on-changed it is the pull-request gate for new code: exit code 1 when any mutant on a changed line
went undetected. The file's score does not matter: old survivors (some reviewed as equivalent, TEST_STRATEGY §11)
never block a change that does not touch their lines. A mutant that is truly equivalent is accepted in the code
with Stryker's own comment, e.g. `// Stryker disable once Equality : <reason>`, so the reason is reviewed in the diff.

Works on both Stryker.NET (backend) and StrykerJS (frontend-web) reports: both use the mutation-testing report
schema. Fails when it cannot map a mutant to the diff, rather than passing a change it could not check.

Reads files and runs `git diff` only.
Usage: python3 -I scripts/mutation_summary.py <mutation-report.json> [<base commit>] [--fail-on-changed]
"""
import json
import pathlib
import re
import subprocess
import sys

DETECTED = {"Killed", "Timeout"}
UNDETECTED = {"Survived", "NoCoverage"}
REPO = pathlib.Path(__file__).resolve().parent.parent
HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", re.M)


def changed_lines(base, path):
    """
    Line numbers of `path` added or changed since `base`, from `git diff -U0`.

    Fails closed: when git cannot diff the file (unknown base, a path outside the repository) the gate cannot tell
    which mutants are new, so it stops instead of reporting "no changed lines" and passing.
    """
    if not base:
        return set()
    if not path.is_file():
        raise SystemExit(f"mutation_summary: the report names {path}, which does not exist. Cannot map mutants "
                         "to changed lines, so the gate cannot decide.")
    try:
        diff = subprocess.run(["git", "diff", "-U0", base, "--", str(path)],
                              capture_output=True, text=True, check=True, cwd=path.parent).stdout
    except (OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"mutation_summary: git diff {base} -- {path} failed ({error}). Cannot map mutants to "
                         "changed lines, so the gate cannot decide.")
    lines = set()
    for start, count in HUNK.findall(diff):
        first, length = int(start), int(count) if count else 1
        lines.update(range(first, first + length))
    return lines


def score(mutants):
    detected = sum(m["status"] in DETECTED for m in mutants)
    tested = detected + sum(m["status"] in UNDETECTED for m in mutants)
    return (100.0 * detected / tested) if tested else None, tested


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    fail_on_changed = "--fail-on-changed" in sys.argv[1:]
    report = json.loads(pathlib.Path(args[0]).read_text(encoding="utf-8"))
    base = args[1] if len(args) > 1 else None
    root = pathlib.Path(report.get("projectRoot") or ".").resolve()

    rows, survivors = [], []
    for name, data in sorted(report["files"].items()):
        mutants = data["mutants"]
        file_score, tested = score(mutants)
        if not tested:
            continue
        # Stryker.NET writes absolute paths, StrykerJS paths relative to its project root (frontend-web).
        path = pathlib.Path(name) if pathlib.Path(name).is_absolute() else root / name
        shown = path.relative_to(REPO).as_posix() if REPO in path.parents else path.as_posix()
        changed = changed_lines(base, path)
        new_gaps = 0
        for m in mutants:
            if m["status"] not in UNDETECTED:
                continue
            line = m["location"]["start"]["line"]
            on_changed = line in changed
            new_gaps += on_changed
            replacement = (m.get("replacement") or "").replace("\n", " ").replace("|", "\\|")[:80]
            survivors.append((not on_changed, shown, line, m["status"], m["mutatorName"], replacement, on_changed))
        rows.append(f"| `{shown}` | {file_score:.1f} % | {tested} | {new_gaps} |")

    print("## Mutation testing of the changed code\n")
    if not rows:
        print("No mutants were tested: the changed files hold no code Stryker mutates.")
        return 0
    all_mutants = [m for f in report["files"].values() for m in f["mutants"]]
    total, tested = score(all_mutants)
    print(f"Score over the changed files: **{total:.1f} %** ({tested} mutants tested).\n")
    print("| File | Score | Mutants tested | Undetected on lines this PR changed |")
    print("|---|---|---|---|")
    print("\n".join(rows))
    gaps = sum(1 for s in survivors if s[-1])
    if base:
        verdict = (f"**{gaps} mutant(s) on lines this pull request changed went undetected.** Add a test that fails "
                   "for each, or accept a truly equivalent one with `// Stryker disable once <Mutator> : <reason>`."
                   if gaps else "Every mutant on a line this pull request changed was detected by a test.")
        print(f"\n{verdict}")
    if survivors:
        print("\n### Mutants no test noticed\n")
        print("A change to the code that every test still passes. Those on lines this pull request changed are")
        print("listed first: they point at a test the change still needs.\n")
        print("| | File:line | Status | Mutation | Replaced by |")
        print("|---|---|---|---|---|")
        for _, shown, line, status, mutator, replacement, on_changed in sorted(survivors):
            mark = "**changed line**" if on_changed else "existing code"
            print(f"| {mark} | `{shown}:{line}` | {status} | {mutator} | `{replacement}` |")
    return 1 if fail_on_changed and gaps else 0


if __name__ == "__main__":
    sys.exit(main())
