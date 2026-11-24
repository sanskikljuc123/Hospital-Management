#!/usr/bin/env python3
"""Gate G4 (d), docs/testing/QUALITY_GATES.md §1: the required checks named in the documents are real CI jobs.

The ruleset *Protect master* requires checks by their name, and QUALITY_GATES lists those names. A job renamed in a
workflow, or a gate added without being written down, makes the two drift apart: then every pull request waits for
a check that never runs, or a gate exists that nobody knows is (not) required. This script fails when

  (a) a check name in backticks in the paragraphs "Enforcement on GitHub" and "Not in the ruleset yet" of
      QUALITY_GATES is not the name of a job in .github/workflows/ (a matrix job counts once per matrix value);
  (b) a job whose name carries a gate number, e.g. "Secret scan (G6)", is not named in QUALITY_GATES.

It cannot read the ruleset itself (that needs a token); the person who changes the ruleset keeps it equal to the
list in QUALITY_GATES. Reads files only. Exit code 1 on a mismatch. Usage: python3 scripts/check_gate_names.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
GATES = ROOT / "docs" / "testing" / "QUALITY_GATES.md"
WORKFLOWS = ROOT / ".github" / "workflows"

JOB_NAME = re.compile(r"^    name: (.+?)\s*$", re.M)  # a job's name: four spaces in, under `jobs:`
MATRIX_REF = re.compile(r"\$\{\{\s*matrix\.(\w+)\s*\}\}")
GATE_NUMBER = re.compile(r"\(G\d+")
PARAGRAPHS = ("**Enforcement on GitHub.**", "**Not in the ruleset yet**")


def job_names():
    """Every check name the workflows produce, matrix jobs expanded."""
    names = set()
    for path in sorted(WORKFLOWS.glob("*.yml")):
        text = path.read_text(encoding="utf-8")
        jobs = text[text.index("\njobs:"):] if "\njobs:" in text else ""
        for raw in JOB_NAME.findall(jobs):
            name = raw.strip("\"'")
            ref = MATRIX_REF.search(name)
            if not ref:
                names.add(name)
                continue
            values = re.search(rf"^\s+{ref.group(1)}: \[([^\]]*)\]", text, re.M)
            for value in (values.group(1).split(",") if values else []):
                names.add(MATRIX_REF.sub(value.strip().strip("\"'"), name))
    return names


def documented_checks(text):
    """Check names in backticks in the enforcement paragraphs (a branch name or a path is not a check name)."""
    checks = set()
    for heading in PARAGRAPHS:
        start = text.index(heading)
        end = text.find("\n\n**", start + len(heading))
        for name in re.findall(r"`([^`]+)`", text[start:end if end != -1 else None]):
            if " " in name and "/" not in name:  # a check name has words; `master` or a path is not one
                checks.add(name)
    return checks


def main():
    text = GATES.read_text(encoding="utf-8")
    jobs = job_names()
    failures = [f"(a) QUALITY_GATES names the check `{name}`, but no workflow job has that name"
                for name in sorted(documented_checks(text) - jobs)]
    failures += [f"(b) the job `{name}` carries a gate number but QUALITY_GATES does not name it"
                 for name in sorted(jobs) if GATE_NUMBER.search(name) and f"`{name}`" not in text]
    print(f"Workflow jobs: {len(jobs)}. Checks named in QUALITY_GATES: {len(documented_checks(text))}.")
    if failures:
        print("\nFAILED:")
        print("\n".join(f"  {f}" for f in failures))
        return 1
    print("Gate names match.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
