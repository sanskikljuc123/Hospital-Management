#!/usr/bin/env python3
"""Quality gates G4 (risk coverage) and G5 (no silent skips), docs/testing/QUALITY_GATES.md §1.

G4 (a) every risk with status `Covered` in docs/testing/RISK_REGISTER.md is named by at least one test
       that is not skipped ([Trait("Risk", "R-xxx")] on the test method or its class);
G4 (b) every Risk trait names a risk that exists in the register;
G4     lists, without failing, the Critical/High risks of level API, Unit or CI that are not Covered yet.
G5     every Skip = "..." names an open risk ("Known gap R-xxx") or a GitHub issue ("#123").

Reads files only. Exit code 1 when a gate fails. Usage: python3 scripts/check_test_gates.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
REGISTER = ROOT / "docs" / "testing" / "RISK_REGISTER.md"
TEST_DIRS = [ROOT / "backend" / "tests", ROOT / "e2e"]

RISK_ROW = re.compile(r"^[|] (R-[0-9]{3}) [|]")
RISK_TRAIT = re.compile(r'Trait[(]"Risk",\s*"(R-[0-9]{3})"[)]')
SKIP = re.compile(r'Skip\s*=\s*"([^"]*)"')
KNOWN_GAP = re.compile(r"Known gap (R-[0-9]{3})")
ISSUE = re.compile(r"#[0-9]+")
DECLARATION = re.compile(r"^\s*(public|internal|private|protected)\b.*\b(class|Task|void)\b")


def read_register():
    """Returns {risk id: (severity, level, status)}."""
    risks = {}
    for line in REGISTER.read_text(encoding="utf-8").splitlines():
        match = RISK_ROW.match(line)
        if not match:
            continue
        cells = [c.strip() for c in line.strip().strip("[|]").split("|")]
        # Columns from the end: ... | Level | Tests | Status |
        severity = next((c.strip("*") for c in cells if re.search(r"(Critical|High|Medium|Low) \(", c)), "")
        risks[match.group(1)] = (severity, cells[-3], cells[-1])
    return risks


def read_tests():
    """Returns a list of (file, line, method risks, skip reason or None) and all skips found."""
    tests, skips = [], []
    for directory in TEST_DIRS:
        if not directory.exists():
            continue
        for path in directory.rglob("*.cs"):
            if any(part in ("bin", "obj") for part in path.parts) or path.name.endswith(".feature.cs"):
                continue  # build output and code generated from .feature files (read below)
            class_risks = set()
            pending = []  # attribute lines since the last declaration
            for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
                stripped = line.strip()
                if stripped.startswith("["):
                    pending.append(stripped)
                    for reason in SKIP.findall(stripped):
                        skips.append((path, number, reason))
                    continue
                if DECLARATION.match(line):
                    block = " ".join(pending)
                    risks = set(RISK_TRAIT.findall(block))
                    if " class " in f" {stripped} ":
                        class_risks = risks
                    elif "Fact" in block or "Theory" in block:
                        skip = SKIP.search(block)
                        tests.append((path, number, risks | class_risks, skip.group(1) if skip else None))
                    pending = []
                elif stripped and not stripped.startswith("//"):
                    pending = []
        tests.extend(read_feature_scenarios(directory))
    return tests, skips


def read_feature_scenarios(directory):
    """Gherkin scenarios (E2E): risk tags such as @R-001 on the feature or the scenario; @ignore skips."""
    scenarios = []
    for path in directory.rglob("*.feature"):
        feature_tags, pending_tags = set(), set()
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            stripped = line.strip()
            if stripped.startswith("@"):
                pending_tags |= set(stripped.split())
            elif stripped.startswith("Feature:"):
                feature_tags, pending_tags = pending_tags, set()
            elif stripped.startswith(("Scenario:", "Scenario Outline:")):
                tags = feature_tags | pending_tags
                risks = {t[1:] for t in tags if re.fullmatch(r"@R-[0-9]{3}", t)}
                scenarios.append((path, number, risks, "ignored" if "@ignore" in tags else None))
                pending_tags = set()
            elif stripped and not stripped.startswith("#"):
                pending_tags = set()
    return scenarios


def main():
    risks = read_register()
    tests, skips = read_tests()
    failures = []

    active = {}
    for path, number, test_risks, skip in tests:
        for risk in test_risks:
            if risk not in risks:
                failures.append(f"G4(b) {rel(path)}:{number} names {risk}, which is not in the register")
            if skip is None:
                active.setdefault(risk, 0)
                active[risk] += 1

    for risk, (_, _, status) in sorted(risks.items()):
        if status == "Covered" and not active.get(risk):
            failures.append(f"G4(a) {risk} is Covered in the register but no active test names it")

    for path, number, reason in skips:
        gap = KNOWN_GAP.search(reason)
        if gap:
            status = risks.get(gap.group(1), (None, None, None))[2]
            if status is None:
                failures.append(f"G5 {rel(path)}:{number} skips for {gap.group(1)}, which is not in the register")
            elif status in ("Covered", "Closed"):
                failures.append(f"G5 {rel(path)}:{number} is still skipped for {gap.group(1)}, which is {status}")
        elif not ISSUE.search(reason):
            failures.append(f'G5 {rel(path)}:{number} Skip = "{reason}" names neither a risk nor an issue')

    uncovered = [
        f"  {risk}  {severity}  level {level}  status {status}  active tests: {active.get(risk, 0)}"
        for risk, (severity, level, status) in sorted(risks.items())
        if severity.startswith(("Critical", "High")) and level in ("API", "Unit", "CI") and status != "Covered"
    ]

    print(f"Register: {len(risks)} risks. Tests: {len(tests)} test methods, {len(skips)} skipped.")
    print(f"Risks named by at least one active test: {len(active)}")
    print("Critical/High risks of level API, Unit or CI not yet Covered (informational):")
    print("\n".join(uncovered) if uncovered else "  none")

    if failures:
        print("\nFAILED:")
        print("\n".join(f"  {f}" for f in failures))
        return 1
    print("\nG4 and G5 passed.")
    return 0


def rel(path):
    return path.relative_to(ROOT).as_posix()


if __name__ == "__main__":
    sys.exit(main())
