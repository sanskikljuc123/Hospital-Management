#!/usr/bin/env python3
"""Gate G4 (e), docs/testing/QUALITY_GATES.md §1: every ID is defined once, and every reference resolves.

The ID legend in docs/testing/README.md ("Identifiers") says where each kind of ID is defined. This script holds
the documents to it:

  defined once  each ID is a row (`| R-069 |`) or, for a flow, a heading (`### F-02`) in the section that owns
                it, and only once there;
  resolves      every ID mentioned in the documents, the agent and skill files and the tests is defined.

A test's flow scenario ([Trait("Scenario", "F-02.6")], @F-02.6 in Gherkin) is a reference like any other, so a
test cannot name a scenario that does not exist. Risk traits are checked by G4 (b), scripts/check_test_gates.py.

D-numbers exist in two series (README, "One prefix, two series"): inside ARCHITECTURE.md a D-number is one of its
decisions (§11); everywhere else a bare D-number is a web defect (UI_TEST_DESIGN §3), and a decision is written
with the word right before it, "ARCHITECTURE D-9".

Known limits: in a range such as "R-001 to R-004" only the two ends are checked; in "ARCHITECTURE D-8, D-9" only
the first number is a decision; a scenario row is not checked against the flow heading above it; the UI test
categories are the fixed list in REFERENCE.

Reads files only. Exit code 1 when an ID is undefined or defined twice, or a file it must read is missing.
Usage: python3 scripts/check_ids.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
TESTING = pathlib.Path("docs/testing")
ARCHITECTURE = pathlib.Path("ARCHITECTURE.md")
UI_CATEGORIES = "HP|NEG|BND|ST|CON|DI|A11Y|RSP|REL|SEC"

# kind: (pattern of the ID, owning document, heading of the owning section)
KINDS = {
    "risk": (r"R-[0-9]{3}", TESTING / "RISK_REGISTER.md", "## 2. Risks"),
    "assumption": (r"A-[0-9]{3}", TESTING / "RISK_REGISTER.md", "## 3. Assumptions"),
    "question": (r"Q-[0-9]{3}", TESTING / "RISK_REGISTER.md", "## 4. Open questions"),
    "ui question": (r"Q-[a-z]", TESTING / "UI_TEST_DESIGN.md", "## 7. Gaps and missing requirements"),
    "defect": (r"D-[0-9]{1,3}", TESTING / "UI_TEST_DESIGN.md", "## 3. Defects found while designing"),
    "decision": (r"D-[0-9]{1,3}", ARCHITECTURE, "## 11. Decision log"),
    "ui test": (rf"UI-(?:{UI_CATEGORIES})-[0-9]{{2}}", TESTING / "UI_TEST_DESIGN.md", "## 6. Test catalogue"),
    "flow": (r"F-[0-9]{2}", TESTING / "BUSINESS_FLOWS.md", "## 3. Flows"),
    "scenario": (r"F-[0-9]{2}\.[0-9]{1,2}", TESTING / "BUSINESS_FLOWS.md", "## 3. Flows"),
    "gate": (r"G[0-9]{1,2}", TESTING / "QUALITY_GATES.md", "## 1. Gates"),
}

# One pattern for every reference. The look-behind and look-ahead keep an ID inside a longer token out (R-0069,
# XR-002, R-002-x, R-002.1), and make F-02.6 match as a scenario rather than as flow F-02 followed by ".6".
REFERENCE = re.compile(
    r"(?<![\w-])("
    rf"UI-(?:{UI_CATEGORIES})-[0-9]{{2}}"
    r"|F-[0-9]{2}\.[0-9]{1,2}|F-[0-9]{2}"
    r"|[RAQ]-[0-9]{3}|Q-[a-z]|D-[0-9]{1,3}|G[0-9]{1,2}"
    r")(?![\w-]|\.[0-9])"
)
DECISION_QUALIFIER = re.compile(r"\bARCHITECTURE\s$")  # "ARCHITECTURE D-9": the word right before the number

# The files that may name an ID, relative to the root. A missing one is a failure, so a rename cannot quietly
# take a file out of the check.
FILES = [ARCHITECTURE, pathlib.Path("docs/API_ENDPOINTS.md"), pathlib.Path("Readme.md"), pathlib.Path("CLAUDE.md")]
DIRECTORIES = [TESTING, pathlib.Path(".claude/agents"), pathlib.Path(".claude/skills"), pathlib.Path("backend/tests"),
               pathlib.Path("e2e"), pathlib.Path("frontend-web/src")]
SKIP_DIRS = {".git", "bin", "obj", "node_modules", "dist", "TestResults", ".vs", "__pycache__", "StrykerOutput",
             ".stryker-tmp"}


def kind_of(identifier, path, before):
    """The kind of an ID; `before` is the text on its line before it. None when it is not an ID this gate checks."""
    if identifier.startswith("D-"):
        return "decision" if path == ARCHITECTURE or DECISION_QUALIFIER.search(before) else "defect"
    for kind, (pattern, _, _) in KINDS.items():
        if kind not in ("defect", "decision") and re.fullmatch(pattern, identifier):
            return kind
    return None


def section_lines(text, heading):
    """The (line number, line) pairs of a level-2 section, from its heading to the next level-2 heading."""
    lines, inside = [], False
    for number, line in enumerate(text.splitlines(), start=1):
        if line.startswith("## "):
            inside = line.strip() == heading
            continue
        if inside:
            lines.append((number, line))
    return lines


def find_definitions(documents):
    """
    documents: {relative path: text}. Returns ({(kind, id): [(path, line)]}, [problems]).
    A problem is an owning section that is missing, so that a renamed heading cannot silently empty a kind.
    """
    definitions, problems = {}, []
    for kind, (pattern, path, heading) in KINDS.items():
        text = documents.get(path)
        if text is None:
            problems.append(f"{path.as_posix()} is missing; it owns every {kind} ID")
            continue
        lines = section_lines(text, heading)
        if not lines:
            problems.append(f"{path.as_posix()} has no section '{heading}'; it owns every {kind} ID")
            continue
        row = re.compile(rf"^\| ({pattern}) \|")
        flow_heading = re.compile(rf"^### ({pattern}) ") if kind == "flow" else None
        for number, line in lines:
            match = row.match(line) or (flow_heading.match(line) if flow_heading else None)
            if match:
                definitions.setdefault((kind, match.group(1)), []).append((path, number))
    return definitions, problems


def find_references(files):
    """files: {relative path: text}. Returns [(kind, id, path, line number)]."""
    references = []
    for path, text in files.items():
        for number, line in enumerate(text.splitlines(), start=1):
            for match in REFERENCE.finditer(line):
                kind = kind_of(match.group(1), path, line[:match.start()])
                if kind:
                    references.append((kind, match.group(1), path, number))
    return references


def check(documents, files):
    """Returns a list of failures; empty when every ID is defined once and every reference resolves."""
    definitions, failures = find_definitions(documents)
    for (kind, identifier), places in sorted(definitions.items()):
        if len(places) > 1:
            where = ", ".join(f"{p.as_posix()}:{n}" for p, n in places)
            failures.append(f"{kind} {identifier} is defined {len(places)} times: {where}")
    for kind, identifier, path, number in find_references(files):
        if (kind, identifier) not in definitions:
            owner, heading = KINDS[kind][1], KINDS[kind][2]
            failures.append(f"{path.as_posix()}:{number} names {kind} {identifier}, which is not defined in "
                            f"{owner.as_posix()} '{heading.lstrip('# ')}'")
    return failures


def is_read(path):
    """
    path: relative to the root. Markdown in the documents, agents and skills; in the test directories the C#
    test sources, Gherkin features, frontend *.test.ts(x) files and the frontend test helpers in src/test/.
    """
    if SKIP_DIRS & set(path.parts):
        return False  # build output and dependencies
    if path.parts[0] in ("docs", ".claude"):
        return path.suffix == ".md"
    if path.name.endswith(".feature.cs"):
        return False  # generated from the .feature file, which is read itself
    if path.suffix in (".ts", ".tsx"):
        return ".test." in path.name or path.parts[:3] == ("frontend-web", "src", "test")
    return path.suffix in (".cs", ".feature")


def read_files(root=ROOT):
    """Every file that may name an ID, as {path relative to root: text}, and the listed files that are missing."""
    found, missing = {}, []
    for path in FILES:
        if (root / path).is_file():
            found[path] = (root / path).read_text(encoding="utf-8")
        else:
            missing.append(f"{path.as_posix()} is missing; G4 (e) reads it")
    for directory in DIRECTORIES:
        if not (root / directory).is_dir():
            missing.append(f"{directory.as_posix()}/ is missing; G4 (e) reads it")
            continue
        for path in sorted((root / directory).rglob("*")):
            relative = path.relative_to(root)
            if path.is_file() and is_read(relative):
                found[relative] = path.read_text(encoding="utf-8")
    return found, missing


def run(root=ROOT):
    """Reads the files under root and returns (files read, failures): missing files first, then the ID check."""
    files, missing = read_files(root)
    documents = {p: t for p, t in files.items() if p.suffix == ".md"}
    return files, missing + check(documents, files)


def main():
    files, failures = run()
    documents = {p: t for p, t in files.items() if p.suffix == ".md"}
    definitions, _ = find_definitions(documents)
    print(f"Files read: {len(files)}. IDs defined: {len(definitions)}. "
          f"References found: {len(find_references(files))}.")
    if failures:
        print("\nFAILED:")
        print("\n".join(f"  {f}" for f in failures))
        return 1
    print("\nG4 (e) passed: every ID is defined once and every reference resolves.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
