"""Compares repeated runs of the same tests and reports the ones whose outcome changed between runs.

Usage: python3 -I scripts/flaky_report.py <results directory>

The directory holds one sub-directory per run (run1, run2, ...), each with the TRX files of that run. A test
that passed in one run and failed in another is flaky (QUALITY_GATES §3); a test that failed in every run is a
plain failure. Prints a Markdown summary and exits 1 when either exists, 0 when every test passed every time.
This only detects: it never re-runs a failing test to make a build green.
"""
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def outcomes(results):
    """Returns ({test name: {run: [outcomes]}}, run names)."""
    found = defaultdict(lambda: defaultdict(list))
    runs = sorted(p.name for p in results.iterdir() if p.is_dir())
    for run in runs:
        for trx in sorted((results / run).rglob("*.trx")):
            # The result file identifies the target framework when a project has multiple targets.
            framework = trx.stem
            for result in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", NS):
                name = f"{result.get('testName')} [{framework}]"
                found[name][run].append(result.get("outcome"))
    return found, runs


def main():
    if len(sys.argv) != 2:
        print(__doc__.strip().splitlines()[2], file=sys.stderr)
        return 2
    sys.stdout.reconfigure(encoding="utf-8")
    found, runs = outcomes(Path(sys.argv[1]))
    flaky, failing = [], []
    for name, per_run in sorted(found.items()):
        seen = {o for run in runs for o in per_run.get(run, ["Missing"])}
        if "Passed" in seen and seen - {"Passed", "NotExecuted"}:
            flaky.append((name, per_run))
        elif seen - {"Passed", "NotExecuted"}:
            failing.append((name, per_run))

    print(f"### Repeated test runs ({len(runs)} runs, {len(found)} tests)")
    print()
    if not runs or not found:
        print("No test results found.")
        return 1
    if not flaky and not failing:
        print("Every test had the same outcome in every run.")
        return 0
    for title, hint, items in (
            ("Flaky: the outcome changed between runs",
             "record it with the flaky-test template (QUALITY_GATES §6); do not re-run to green", flaky),
            ("Failed in every run",
             "a failure on master: classify it as product defect, test defect or environment (QUALITY_GATES §4)",
             failing)):
        if not items:
            continue
        print(f"**{title}** — {hint}.")
        print()
        print("| Test | " + " | ".join(runs) + " |")
        print("|---|" + "---|" * len(runs))
        for name, per_run in items:
            cells = [", ".join(per_run.get(run, ["Missing"])) for run in runs]
            print(f"| {name} | " + " | ".join(cells) + " |")
        print()
    return 1


if __name__ == "__main__":
    sys.exit(main())
