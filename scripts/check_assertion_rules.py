#!/usr/bin/env python3
"""Quality gate G4 (g): the assertion rules a script can check (docs/testing/TEST_STRATEGY.md §4, Assertion rules).

The 20 rules are mandatory for every test. Most need judgement (is this the business result? is the oracle
independent?) and are checked in review by qe-reviewer and the code owner. These five show in the text of a test
and are checked here, on every pull request:

  rule 7   Assert.All over a collection, with no NotEmpty, Single, Contains or Equal on that collection earlier in
           the same test (Assert.All passes on an empty collection).
  rule 12  IsCompleted asserted with no IsFaulted, IsCompletedSuccessfully or RanToCompletion next to it
           (IsCompleted is also true for a faulted task).
  rule 15  an assertion inside an if, else, switch, case, try or catch: it may not run. Assert.Fail is allowed
           there, because a failure path is the point of such a branch.
  rule 16  a count compared with > or >= in Assert.True / False, toBeGreaterThan / toBeLessThan in a component
           test, and toMatchObject on a submitted body (mock.calls, a received request body).
  rule 19  Assert.True / Assert.False without a message.

A finding is accepted only with a comment on or up to ten lines above it:
    // Assertion rule N exception: <reason of at least ten characters>
The comment itself is checked too: a rule number outside 1-20 or a missing reason is a finding.

Heuristics on text, not a compiler: a finding names the line, the rule and what to change. Reads files only.
Exit code 1 when a rule is broken. Usage: python3 -I scripts/check_assertion_rules.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKIPPED_DIRS = {"bin", "obj", "node_modules", ".git", "worktrees"}

EXCEPTION = re.compile(r"Assertion rule (\d+) exception:(.*)")
EXCEPTION_WINDOW = 10
ASSERTION = re.compile(r"\bAssert\.(?!Fail\b)\w+\s*\(|\bexpect\s*\(|\bAssertions\.Expect\s*\(")
CONDITIONAL_HEADER = re.compile(r"^\s*(?:\}\s*)?(?:if\b|else\b|switch\b|case\b|default\s*:|try\b|catch\b)")
METHOD_START = re.compile(r"^\s*(?:\[(?:Fact|Theory|Given|When|Then)\b|(?:public|private|internal|protected)\s.*\()"
                          r"|^\s*(?:it|test)(?:\.\w+)*\s*\(")
NON_EMPTY_PROOF = r"\bAssert\.(?:NotEmpty|Single|Contains|Equal)\s*\("
COUNT = re.compile(r"\b(?:Count|Length|GetArrayLength)\b|\.Data\[")
COMPARISON = re.compile(r"(?<![=<>!])(?:>=|<=|>|<)(?![=>])")


def source_files(root=ROOT):
    """The test sources: backend and E2E C#, frontend component tests."""
    patterns = [("backend/tests", "*.cs"), ("e2e", "*.cs"), ("frontend-web/src", "*.test.ts"),
                ("frontend-web/src", "*.test.tsx")]
    for folder, pattern in patterns:
        for path in sorted((root / folder).rglob(pattern)):
            if not SKIPPED_DIRS.intersection(path.relative_to(root).parts):
                yield path


def code_only(line):
    """The line without string literals and without a // comment, so a quote or a brace inside text is ignored."""
    out, i, quote = [], 0, None
    while i < len(line):
        c = line[i]
        if quote:
            if c == "\\":
                i += 2
                continue
            if c == quote:
                quote = None
                out.append('""')
        elif c in "\"'`":
            quote = c
        elif line.startswith("//", i):
            break
        else:
            out.append(c)
        i += 1
    return "".join(out)


def arguments(text, open_index):
    """The top-level arguments of the call whose '(' is at open_index, or None when the call does not close."""
    depth, quote, args, current, i = 0, None, [], [], open_index
    while i < len(text):
        c = text[i]
        if quote:
            current.append(c)
            if c == "\\":
                current.append(text[i + 1] if i + 1 < len(text) else "")
                i += 2
                continue
            if c == quote:
                quote = None
        elif c in "\"'`":
            quote = c
            current.append(c)
        elif c in "([{":
            depth += 1
            if depth > 1:
                current.append(c)
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                args.append("".join(current).strip())
                return [a for a in args if a]
            current.append(c)
        elif c == "," and depth == 1:
            args.append("".join(current).strip())
            current = []
        else:
            current.append(c)
        i += 1
    return None


def exceptions(lines):
    """({line index: rules excepted there}, findings about malformed exception comments)."""
    excepted, findings = {}, []
    for index, line in enumerate(lines):
        match = EXCEPTION.search(line)
        if not match:
            continue
        rule, reason = int(match.group(1)), match.group(2).strip()
        if not 1 <= rule <= 20:
            findings.append((index, "exception", f"names rule {rule}; the rules are 1-20 (TEST_STRATEGY, Assertion rules)"))
            continue
        if len(reason) < 10:
            findings.append((index, "exception", f"an exception to rule {rule} needs its reason after the colon"))
            continue
        for covered in range(index, min(index + EXCEPTION_WINDOW + 1, len(lines))):
            excepted.setdefault(covered, set()).add(rule)
    return excepted, findings


def conditional_lines(lines):
    """The indexes of lines that run only on one branch: inside if/else/switch/case/try/catch."""
    stack, result, pending = [], set(), False  # pending: the next statement is the body of a branch
    for index, raw in enumerate(lines):
        code = code_only(raw)
        stripped = code.strip()
        if not stripped:
            continue
        header = bool(CONDITIONAL_HEADER.match(code))
        opens_branch = header
        if pending:
            if stripped.startswith("{"):
                opens_branch = True  # the brace of "if (x)" on the line before (Allman style)
            else:
                result.add(index)  # "if (x)" followed by a single statement
            pending = False
        if any(kind == "branch" for kind in stack):
            result.add(index)
        if header:
            # What follows the header on its own line: "if (x) Assert.Equal(...);" or "case 1: Assert...".
            body = re.sub(r"^\s*(?:\}\s*)?(?:else\s+if|if|switch|catch)\s*\(.*\)", "", code)
            body = re.sub(r"^\s*(?:\}\s*)?(?:else|try|catch|default\s*:|case\b[^:]*:)", "", body).strip()
            body = re.sub(r"^when\s*\(.*\)", "", body).strip()
            if body and not body.startswith("{"):
                result.add(index)
            elif not body:
                pending = True
        for c in code:
            if c == "{":
                stack.append("branch" if opens_branch else "block")
                opens_branch = False
            elif c == "}" and stack:
                stack.pop()
    return result


def method_start(lines, index):
    for back in range(index, max(-1, index - 80), -1):
        if METHOD_START.search(lines[back]):
            return back
    return max(0, index - 80)


def check_file(path, text):
    lines = text.splitlines()
    excepted, findings = exceptions(lines)
    conditional = conditional_lines(lines)

    for index, line in enumerate(lines):
        code = code_only(line)

        if index in conditional and ASSERTION.search(code):
            findings.append((index, 15, "assertion inside a branch that may not run; use data rows or separate tests"))

        if ".IsCompleted" in code and ASSERTION.search(code):
            nearby = " ".join(lines[max(0, index - 3): index + 4])
            if not re.search(r"IsFaulted|IsCompletedSuccessfully|RanToCompletion", nearby):
                findings.append((index, 12, "IsCompleted is also true for a faulted task; assert IsFaulted is false too"))

        if path.suffix == ".cs":
            for call in re.finditer(r"\bAssert\.(True|False)\s*\(", line):
                args = arguments("\n".join(lines[index:index + 15]), call.end() - 1 + 0)
                if args is not None and len(args) < 2:
                    findings.append((index, 19, f"Assert.{call.group(1)} without a message: say what was expected, "
                                                "or use Equal / Single / Contains"))
                if args and COUNT.search(args[0]) and COMPARISON.search(code_only(args[0])):
                    findings.append((index, 16, "a count compared with > or >=: assert the exact count"))

            for call in re.finditer(r"\bAssert\.All\s*\(", line):
                args = arguments("\n".join(lines[index:index + 15]), call.end() - 1)
                root = re.match(r"\s*(?:await\s+)?\(?\s*(?:await\s+)?([A-Za-z_]\w*)", args[0]) if args else None
                if not root:
                    continue
                start = method_start(lines, index)
                proof = re.compile(NON_EMPTY_PROOF + r"[^;]*\b" + re.escape(root.group(1)) + r"\b")
                if not any(proof.search(l) for l in lines[start:index]):
                    findings.append((index, 7, f"Assert.All over {root.group(1)} passes on an empty collection; "
                                               "assert NotEmpty, Single or Contains on it first"))
        else:
            if re.search(r"\.(?:toBeGreaterThan|toBeGreaterThanOrEqual|toBeLessThan|toBeLessThanOrEqual)\s*\(", code):
                findings.append((index, 16, "a bound instead of the exact value: use toBe / toHaveLength"))
            if ".toMatchObject(" in code:
                target = " ".join(lines[max(0, index - 2): index + 1])
                if re.search(r"mock\.calls|received|body", target):
                    findings.append((index, 16, "toMatchObject on a submitted body accepts missing and extra fields; "
                                                "use toEqual on the whole body"))

    return [(index, rule, message) for index, rule, message in findings
            if rule == "exception" or rule not in excepted.get(index, set())]


def check(files):
    """files: {path relative to the root: text}. Returns the findings as printable lines."""
    result = []
    for path, text in sorted(files.items()):
        for index, rule, message in sorted(check_file(pathlib.Path(path), text), key=lambda f: f[0]):
            label = "exception comment" if rule == "exception" else f"rule {rule}"
            result.append(f"G4(g) {pathlib.PurePosixPath(path)}:{index + 1} {label}: {message}")
    return result


def main():
    files = {path.relative_to(ROOT).as_posix(): path.read_text(encoding="utf-8") for path in source_files()}
    findings = check(files)
    for finding in findings:
        print(finding)
    if findings:
        print(f"\nG4 (g) failed: {len(findings)} assertion-rule finding(s). Fix the test, or add "
              "'// Assertion rule N exception: <reason>' where the rule truly does not apply (TEST_STRATEGY, Assertion rules).")
        return 1
    print(f"G4 (g) passed: {len(files)} test files, rules 7, 12, 15, 16 and 19 checked.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
