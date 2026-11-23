"""Lists every rating and status change in docs/testing/RISK_REGISTER.md between a base commit and the working tree.

Usage: python3 -I scripts/register_changes.py <base commit>

Prints a Markdown table for the pull request's job summary, so the person who merges sees each change of
Impact, Likelihood, Severity, Level or Status, and every added or removed risk, without reading the whole diff.
It never fails the build: a rating changes only when the test owner changes it (RISK_REGISTER §1), and this
makes such a change impossible to miss (QUALITY_GATES G8). Exit code 0 unless the arguments are wrong.
"""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REGISTER = "docs/testing/RISK_REGISTER.md"
ROW = re.compile(r"^\|\s*(R-\d{3})\s*\|")
# Risk table: | ID | Risk | STRIDE | I | L | Severity | Evidence | Level | Tests | Status |
FIELDS = {"I": 3, "L": 4, "Severity": 5, "Level": 7, "Status": 9}
COLUMNS = 10


def parse(text):
    """Returns {risk id: {field: value}} for the rows of the risk tables (not the accepted-risks table)."""
    risks = {}
    for line in text.splitlines():
        match = ROW.match(line)
        if not match:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) != COLUMNS:
            continue
        risks[match.group(1)] = {name: cells[i].replace("**", "") for name, i in FIELDS.items()}
    return risks


def base_register(base):
    result = subprocess.run(["git", "show", f"{base}:{REGISTER}"], cwd=ROOT, capture_output=True, text=True,
                            encoding="utf-8")
    return result.stdout if result.returncode == 0 else ""


def changes(before, after):
    rows = []
    for risk in sorted(set(before) | set(after)):
        old, new = before.get(risk), after.get(risk)
        if old is None:
            rows.append((risk, "added", "–", f"{new['Severity']}, {new['Level']}, {new['Status']}"))
        elif new is None:
            rows.append((risk, "removed", f"{old['Severity']}, {old['Level']}, {old['Status']}", "–"))
        else:
            rows.extend((risk, name, old[name], new[name]) for name in FIELDS if old[name] != new[name])
    return rows


def main():
    if len(sys.argv) != 2:
        print(__doc__.strip().splitlines()[2], file=sys.stderr)
        return 2
    sys.stdout.reconfigure(encoding="utf-8")
    after = parse((ROOT / REGISTER).read_text(encoding="utf-8"))
    rows = changes(parse(base_register(sys.argv[1])), after)

    print("### Risk register changes")
    print()
    if not rows:
        print("No rating, level or status changed in this pull request.")
        return 0
    print("Each change below is the test owner's decision. A risk set to `Covered` needs red evidence in the")
    print("pull request description (QUALITY_GATES §2).")
    print()
    print("| Risk | Field | Before | After |")
    print("|---|---|---|---|")
    for risk, field, old, new in rows:
        print(f"| {risk} | {field} | {old} | {new} |")
    return 0


if __name__ == "__main__":
    sys.exit(main())
