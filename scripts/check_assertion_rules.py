#!/usr/bin/env python3
"""Quality gate G4 (g): the assertion rules a script can check (docs/testing/TEST_STRATEGY.md §4, Assertion rules).

The 20 rules are mandatory for every test. Most need judgement (is this the business result? is the oracle
independent?) and are checked in review by qe-reviewer and the code owner. These five show in the text of a test
and are checked here, on every pull request:

  rule 7   Assert.All, or a foreach (for...of) with an assertion in it, over a collection with no NotEmpty, Single,
           Contains, or Equal to a non-zero value on that collection earlier in the test: both pass on an empty one.
  rule 12  IsCompleted asserted with no IsFaulted, IsCompletedSuccessfully or RanToCompletion asserted next to it
           (IsCompleted is also true for a faulted task).
  rule 15  an assertion inside if / else / switch / case / catch, or inside a try that has a catch (the catch can
           swallow its failure), and an early return / continue inside a branch with assertions after it. A
           try/finally is not a branch. Assert.Fail is allowed in a branch: a failure path is the point of it.
  rule 16  a lower bound where the exact value is known: > or >= (or a number < / <= the value) in Assert.True /
           False, Assert.NotEqual(0, ...), toBeGreaterThan(OrEqual) in a component test; and a partial body:
           toMatchObject on a submitted body (mock.calls, a received request body) or expect.objectContaining.
  rule 19  Assert.True / Assert.False without a message, or with an empty or null one.

A finding is accepted only with a comment on the assertion's line or directly above it (it covers the next
assertion within ten lines, and only that one):
    // Assertion rule N exception: <reason of at least ten characters>
The comment itself is checked too: a rule number outside 1-20 or a missing reason is a finding.

Heuristics on text, not a compiler. Known limits: an assertion inside a for or while loop is not flagged; a value
copied into a variable before it is asserted (var done = task.IsCompleted) is not followed; a TypeScript regular
expression literal containing a quote can hide the rest of its line. Reads files only. Exit code 1 when a rule is
broken. Usage: python3 -I scripts/check_assertion_rules.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKIPPED_DIRS = {"bin", "obj", "node_modules", ".git", "worktrees"}

EXCEPTION = re.compile(r"Assertion rule (\d+) exception:(.*)")
EXCEPTION_WINDOW = 10
ASSERTION = re.compile(r"\bAssert\.(?!Fail\b)\w+\s*\(|\bexpect\s*\(|\bAssertions\.Expect\s*\(")
METHOD_START = re.compile(r"^\s*(?:\[(?:Fact|Theory|Given|When|Then)\b|(?:public|private|internal|protected)\s.*\()"
                          r"|^\s*(?:it|test)(?:\.\w+)*\s*\(")
HEADER = re.compile(r"^(else\s+if|if|switch|catch|foreach|for|while|else|try|finally|do|case\b[^:]*:|default\s*:)")
COUNT = re.compile(r"\b(?:Count|Length|GetArrayLength)\b|\.Data\[")
LOWER_BOUND = re.compile(r"(?:>=|>(?!=))\s*-?\d|\d\s*(?:<=|<(?!=))")
GENERIC = re.compile(r"(?<=\w)<[\w\s.,?\[\]]+>")


def source_files(root=ROOT):
    """The test sources: backend and E2E C#, frontend component tests."""
    patterns = [("backend/tests", "*.cs"), ("e2e", "*.cs"), ("frontend-web/src", "*.test.ts"),
                ("frontend-web/src", "*.test.tsx")]
    for folder, pattern in patterns:
        for path in sorted((root / folder).rglob(pattern)):
            if not SKIPPED_DIRS.intersection(path.relative_to(root).parts):
                yield path


def code_mask(line):
    """For each character of the line: True when it is code, False inside a string literal or a // comment.
    Handles C# verbatim strings (@"...", where a backslash is literal and "" is a quote)."""
    mask, i, quote, verbatim = [True] * len(line), 0, None, False
    while i < len(line):
        c = line[i]
        if quote:
            mask[i] = False
            if verbatim and c == '"' and line[i + 1:i + 2] == '"':
                mask[i + 1] = False
                i += 2
                continue
            if not verbatim and c == "\\":
                if i + 1 < len(line):
                    mask[i + 1] = False
                i += 2
                continue
            if c == quote:
                quote, verbatim = None, False
        elif line.startswith("//", i):
            for j in range(i, len(line)):
                mask[j] = False
            break
        elif c in "\"'`":
            quote, verbatim = c, i > 0 and line[i - 1] == "@" and c == '"'
            mask[i] = False
        i += 1
    return mask


def code_only(line):
    """The line with string literals replaced by "" and without its // comment."""
    mask, out, in_string = code_mask(line), [], False
    for c, is_code in zip(line, mask):
        if is_code:
            if in_string:
                out.append('""')
                in_string = False
            out.append(c)
        elif not in_string and c in "\"'`" and not line.lstrip().startswith("//"):
            in_string = True
    if in_string:
        out.append('""')
    text = "".join(out)
    comment = line.find("//")
    while comment >= 0 and mask[comment] if comment < len(mask) else False:
        comment = line.find("//", comment + 1)
    return text


def comment_text(line):
    """The // comment of the line, or ''."""
    mask = code_mask(line)
    for i in range(len(line) - 1):
        if line.startswith("//", i) and not mask[i] and (i == 0 or mask[i - 1] or line[:i].strip() == ""):
            return line[i:]
    return ""


def arguments(text, open_index):
    """The top-level arguments of the call whose '(' is at open_index, or None when the call does not close."""
    depth, quote, args, current, i = 0, None, [], [], open_index
    while i < len(text):
        c = text[i]
        if quote:
            current.append(c)
            if c == "\\" and quote != "@":
                current.append(text[i + 1] if i + 1 < len(text) else "")
                i += 2
                continue
            if c == '"' and quote == "@" and text[i + 1:i + 2] == '"':
                current.append('"')
                i += 2
                continue
            if c == (quote if quote != "@" else '"'):
                quote = None
        elif c in "\"'`":
            quote = "@" if c == '"' and i > 0 and text[i - 1] == "@" else c
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


def matching_paren(code, open_index):
    """The index of the ')' that closes code[open_index], or None when it closes on a later line."""
    depth = 0
    for i in range(open_index, len(code)):
        if code[i] == "(":
            depth += 1
        elif code[i] == ")":
            depth -= 1
            if depth == 0:
                return i
    return None


def header_kind(keyword, condition):
    keyword = keyword.split()[0] if not keyword.startswith(("case", "default")) else keyword
    if keyword in ("if", "else", "switch", "catch") or keyword.startswith(("case", "default")):
        return "branch"
    if keyword == "try":
        return "try"
    if keyword == "foreach" or (keyword == "for" and re.search(r"\b(?:of|in)\b", condition or "")):
        root = re.search(r"\b(?:in|of)\s+(?:await\s+)?([A-Za-z_]\w*)", condition or "")
        return "foreach:" + (root.group(1) if root else "")
    return "block"  # for, while, do, finally


def statement_contexts(lines):
    """For each line: the set of block kinds it runs inside ('branch', 'try', 'foreach:<root>').
    Also returns the try blocks as (first line, last line, has a catch)."""
    stack = []  # (kind, first line index)
    contexts, tries = {}, []
    pending = None  # the kind the next statement or '{' belongs to
    open_header = None  # (kind keyword, condition so far, paren depth) of a condition spread over lines
    codes = [code_only(line) for line in lines]

    def push(kind, index):
        stack.append((kind, index))

    def pop(index):
        if not stack:
            return
        kind, start = stack.pop()
        if kind == "try":
            following = next((codes[j].strip() for j in range(index + 1, len(codes)) if codes[j].strip()), "")
            on_line = codes[index].split("}", 1)[1].strip() if "}" in codes[index] else ""
            tries.append((start, index, (on_line or following).startswith("catch")))

    def handle_rest(kind, rest, index, own):
        nonlocal pending
        rest = rest.strip()
        if not rest:
            pending = kind
        elif rest.startswith("{"):
            braces(rest, index, first=kind)
        else:
            own.add(kind)  # if (x) Assert.Equal(...);  case 1: Assert...
            braces(rest, index)

    def braces(text, index, first=None):
        for c in text:
            if c == "{":
                push(first or "block", index)
                first = None
            elif c == "}":
                pop(index)

    for index, code in enumerate(codes):
        stripped = code.strip()
        own = set()
        if not stripped:
            contexts[index] = {kind for kind, _ in stack}
            continue
        if open_header:
            keyword, condition, _ = open_header
            close = matching_paren("(" + condition + "\n" + stripped, 0)
            if close is None:
                open_header = (keyword, condition + "\n" + stripped, 0)
                contexts[index] = {kind for kind, _ in stack}
                continue
            full = ("(" + condition + "\n" + stripped)
            kind = header_kind(keyword, full[1:close])
            open_header = None
            contexts[index] = {k for k, _ in stack}
            handle_rest(kind, full[close + 1:], index, own)
            contexts[index] |= own
            continue

        if pending:
            kind, pending = pending, None
            if stripped.startswith("{"):
                contexts[index] = {k for k, _ in stack}
                braces(stripped, index, first=kind)
                continue
            own.add(kind)  # if (x) on the line before, then a single statement

        while stripped.startswith("}"):
            pop(index)
            stripped = stripped[1:].lstrip()
        contexts[index] = {k for k, _ in stack}

        header = HEADER.match(stripped)
        if header:
            keyword = header.group(1)
            rest = stripped[header.end():]
            if keyword.split()[0] in ("if", "switch", "catch", "foreach", "for", "while") or keyword == "else if":
                open_paren = rest.find("(")
                if open_paren < 0:
                    kind = header_kind(keyword, "")
                    handle_rest(kind, rest, index, own)
                else:
                    close = matching_paren(rest, open_paren)
                    if close is None:
                        open_header = (keyword, rest[open_paren + 1:], 0)
                    else:
                        kind = header_kind(keyword, rest[open_paren + 1:close])
                        after = re.sub(r"^\s*when\s*\(.*\)", "", rest[close + 1:])
                        handle_rest(kind, after, index, own)
            else:
                handle_rest(header_kind(keyword, ""), rest, index, own)
        else:
            braces(stripped, index)
        # A branch that starts later on the same line: "if (x) { y = 1; } else { Assert.Equal(1, y); }".
        later = re.search(r"(?<=[;}\s])(?:else\b|if\s*\(|catch\b|case\b[^:]*:)", stripped[1:])
        if later and ASSERTION.search(stripped[1 + later.end():]):
            own.add("branch")
        contexts[index] |= own
    return contexts, tries


def method_bounds(lines, index):
    start = next((b for b in range(index, max(-1, index - 80), -1) if METHOD_START.search(lines[b])),
                 max(0, index - 80))
    end = next((a for a in range(index + 1, min(len(lines), index + 80)) if METHOD_START.search(lines[a])),
               min(len(lines), index + 80))
    return start, end


def proves_content(code, root):
    """Does this line assert that the collection `root` has content (NotEmpty, Single, Contains, Equal to non-zero)?"""
    for call in re.finditer(r"\bAssert\.(NotEmpty|Single|Contains|Equal)\s*\(", code):
        args = arguments(code, call.end() - 1) or []
        mentions = [a for a in args if re.search(rf"\b{re.escape(root)}\b", a)]
        if not mentions:
            continue
        if call.group(1) != "Equal":
            return True
        expected = args[0].strip() if args else ""
        if expected != "0" and not re.search(rf"\b{re.escape(root)}\b", expected) and re.search(
                r"Count|Length|GetArrayLength", " ".join(mentions)) or expected.startswith(("new", "[")):
            return True
    if re.search(rf"\bexpect\(\s*{re.escape(root)}\b[^)]*\)\.(?:toHaveLength\((?!0\))|toContain|toContainEqual)", code):
        return True
    return False


def exceptions(lines, codes):
    """({line index of the excepted assertion: rules}, findings about malformed exception comments)."""
    excepted, findings = {}, []
    for index, line in enumerate(lines):
        match = EXCEPTION.search(comment_text(line))
        if not match:
            continue
        rule, reason = int(match.group(1)), match.group(2).strip()
        if not 1 <= rule <= 20:
            findings.append((index, "exception", f"names rule {rule}; the rules are 1-20 (TEST_STRATEGY, Assertion rules)"))
            continue
        if len(reason) < 10:
            findings.append((index, "exception", f"an exception to rule {rule} needs its reason after the colon"))
            continue
        target = next((j for j in range(index, min(index + EXCEPTION_WINDOW + 1, len(lines)))
                       if ASSERTION.search(codes[j]) or "Assert.All" in codes[j] or codes[j].lstrip().startswith("foreach")),
                      None)
        if target is not None:
            excepted.setdefault(target, set()).add(rule)
    return excepted, findings


def check_file(path, text):
    lines = text.splitlines()
    codes = [code_only(line) for line in lines]
    excepted, findings = exceptions(lines, codes)
    contexts, tries = statement_contexts(lines)
    caught = {i for start, end, has_catch in tries if has_catch for i in range(start, end + 1)}
    flagged_loops = set()

    for index, code in enumerate(codes):
        context = contexts.get(index, set())
        is_assertion = bool(ASSERTION.search(code))

        # rule 15: branches, try with catch, early exits
        if is_assertion and ("branch" in context or index in caught):
            reason = ("assertion inside a try that has a catch: the catch can swallow its failure" if index in caught
                      and "branch" not in context else "assertion inside a branch that may not run")
            findings.append((index, 15, reason + "; use data rows or separate tests"))
        if "branch" in context and re.search(r"\b(?:return|continue)\s*;", code):
            _, end = method_bounds(lines, index)
            if any(ASSERTION.search(codes[j]) for j in range(index + 1, end)):
                findings.append((index, 15, "an early return skips the assertions after it; use separate tests"))

        # rule 7: foreach over a collection not proven non-empty
        for kind in context:
            if kind.startswith("foreach:") and is_assertion:
                root = kind.split(":", 1)[1]
                loop_line = next((j for j in range(index, -1, -1) if re.match(r"\s*(?:foreach|for)\b", codes[j])), index)
                if root and loop_line not in flagged_loops:
                    start, _ = method_bounds(lines, loop_line)
                    if not any(proves_content(codes[j], root) for j in range(start, loop_line)):
                        flagged_loops.add(loop_line)
                        findings.append((loop_line, 7, f"a loop with assertions over {root} passes on an empty "
                                                       "collection; assert NotEmpty, Single or Contains on it first"))

        # rule 12
        if ".IsCompleted" in code and is_assertion:
            nearby = [codes[j] for j in range(max(0, index - 3), min(len(codes), index + 4)) if ASSERTION.search(codes[j])]
            if not any(re.search(r"IsFaulted|IsCompletedSuccessfully|RanToCompletion|\.Status\b", c) for c in nearby):
                findings.append((index, 12, "IsCompleted is also true for a faulted task; assert IsFaulted is false too"))

        if path.suffix == ".cs":
            joined = "\n".join(lines[index:index + 15])
            mask = code_mask(lines[index])
            for call in re.finditer(r"\bAssert\.(True|False)\s*\(", lines[index]):
                if not mask[call.start()]:
                    continue  # inside a string or a comment
                args = arguments(joined, call.end() - 1)
                if args is not None and (len(args) < 2 or args[1].strip() in ('""', "null", "string.Empty", "''")):
                    findings.append((index, 19, f"Assert.{call.group(1)} without a message: say what was expected, "
                                                "or use Equal / Single / Contains"))
                if args:
                    condition = GENERIC.sub("", code_only(args[0]))
                    if LOWER_BOUND.search(condition) or (COUNT.search(condition) and re.search(r">=?", condition)):
                        findings.append((index, 16, "a lower bound where the exact value is known: assert the exact value"))
            if re.search(r"\bAssert\.NotEqual\s*\(\s*0\s*,", code):
                findings.append((index, 16, "NotEqual(0, ...) is a lower bound: assert the exact value"))

            for call in re.finditer(r"\bAssert\.All\s*\(", code):
                args = arguments("\n".join(codes[index:index + 15]), call.end() - 1)
                first = args[0].strip() if args else ""
                if first.startswith(("new", "[")):
                    continue  # a literal collection is not empty
                root = re.match(r"(?:await\s+)?\(?\s*(?:await\s+)?([A-Za-z_]\w*)", first)
                if not root:
                    continue
                start, _ = method_bounds(lines, index)
                if not any(proves_content(codes[j], root.group(1)) for j in range(start, index)):
                    findings.append((index, 7, f"Assert.All over {root.group(1)} passes on an empty collection; "
                                               "assert NotEmpty, Single or Contains on it first"))
        else:
            if re.search(r"\.(?:toBeGreaterThan|toBeGreaterThanOrEqual)\s*\(", code):
                findings.append((index, 16, "a lower bound where the exact value is known: use toBe / toHaveLength"))
            if "expect.objectContaining(" in code:
                findings.append((index, 16, "expect.objectContaining accepts missing and extra fields; "
                                            "compare the whole body"))
            if ".toMatchObject(" in code:
                target = re.search(r"expect\(\s*([^)]*)\)\s*\.toMatchObject", code)
                subject = target.group(1) if target else ""
                body_like = r"mock\.calls|received|\bbody\b|request\.json"
                variable = re.fullmatch(r"[A-Za-z_]\w*", subject.strip())
                assigned = variable and any(
                    re.search(rf"\b(?:const|let|var)\s+{variable.group(0)}\s*=.*(?:{body_like})", codes[j])
                    for j in range(max(0, index - 15), index))
                if re.search(body_like, subject) or assigned:
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
              "'// Assertion rule N exception: <reason>' where the rule truly does not apply (TEST_STRATEGY, "
              "Assertion rules).")
        return 1
    print(f"G4 (g) passed: {len(files)} test files, rules 7, 12, 15, 16 and 19 checked.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
