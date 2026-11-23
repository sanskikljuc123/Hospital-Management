#!/usr/bin/env python3
"""Gate G4 (c), docs/testing/QUALITY_GATES.md §1: the evidence the documents cite still exists.

Agents and people rely on citations in the agent definitions, the skills and the testing documents. When code
moves, a citation can quietly point at nothing: the agents once cited helpers that had been removed. This script
fails when a cited path, file, line or code name no longer exists. It checks that a citation *resolves*, not that
the cited line still says the same thing; reviewers and qe-reviewer (check V3) still read the code.

Checked, in `backticks`:
  paths       `backend/api/Auth/` `Support/TestSupport.cs` `Policies.cs`    -> a file or folder with that path, or
                                                                              ending in it, or with that name
  lines       `AppointmentService.cs:52, 90, 299` `Program.cs:47`        -> a file of that name has that many lines;
              `:121-123` after a file on the same line refers to that file
  code names  `GetCurrentDoctorIdAsync` `TestDb.Create()` (agents and skills only) -> each part appears in the code

Reads files only. Exit code 1 when a reference is broken. Usage: python3 scripts/check_doc_references.py
"""
import os
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKIP_DIRS = {".git", "bin", "obj", "node_modules", "dist", "TestResults", ".vs", "__pycache__", "StrykerOutput"}
# Local git worktrees of Claude Code sessions: other checkouts, not this one. Never in CI; skipped so a local run
# (scripts/verify.sh) neither checks their copies of the documents nor resolves a citation against them.
WORKTREES = ROOT / ".claude" / "worktrees"

DOCUMENTS = (
    [p for p in (ROOT / ".claude").rglob("*.md") if WORKTREES not in p.parents]
    + list((ROOT / "docs" / "testing").glob("*.md"))
    + [ROOT / "docs" / "API_ENDPOINTS.md", ROOT / "ARCHITECTURE.md"]
)
# Agent and skill files also get their code names checked; elsewhere backticks hold too many plain words.
# Relative to the root: a checkout that itself lives under a .claude folder (a session worktree) is not an agent file.
SYMBOL_DOCUMENTS = {p for p in DOCUMENTS if p.relative_to(ROOT).parts[0] == ".claude"}
CODE_DIRS = ["backend", "e2e", "frontend-web/src", "keycloak", "scripts", ".github"]
CODE_SUFFIXES = {".cs", ".ts", ".tsx", ".feature", ".json", ".yml", ".yaml", ".py", ".csproj"}

# Format examples and placeholders that are not meant to resolve.
EXAMPLES = {
    "path/file.cs:line", "file:line", "backend/api/Controllers/X.cs:42", "Method_or_Endpoint_Condition_ExpectedResult",
}
# Named on purpose although they are gone: the register records that they were deleted (R-030, Q-009).
DELETED_ON_PURPOSE = {"AppDesktop/", "GestionHopitalSQL/"}
FILE_SUFFIXES = (".md", ".cs", ".yml", ".yaml", ".json", ".ts", ".tsx", ".py", ".feature", ".csproj", ".sln", ".ps1", ".sh")

TICKED = re.compile(r"`([^`\n]+)`")
LINE_CITATION = re.compile(r"^(?P<file>[\w./-]+\.\w+)?:(?P<lines>\d+(?:\s*[-–]\s*\d+)?(?:\s*,\s*\d+(?:\s*[-–]\s*\d+)?)*)$")
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
CODE_NAME = re.compile(r"^[A-Za-z_][\w]*(?:\.[A-Za-z_]\w*)*(?:\([^)]*\))?(?:/[A-Za-z_][\w]*(?:\([^)]*\))?)*$")


def walk_files():
    for directory, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and pathlib.Path(directory, d) != WORKTREES]
        for name in files:
            yield pathlib.Path(directory, name)


def index_repository():
    paths, by_name, directories = set(), {}, set()
    for path in walk_files():
        relative = path.relative_to(ROOT).as_posix()
        paths.add(relative)
        by_name.setdefault(path.name, []).append(path)
        parent = path.parent
        while parent != ROOT:
            directories.add(parent.relative_to(ROOT).as_posix())
            parent = parent.parent
    return paths, by_name, directories


def index_code_words():
    words = set()
    for code_dir in CODE_DIRS:
        base = ROOT / code_dir
        if not base.exists():
            continue
        for directory, dirs, files in os.walk(base):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for name in files:
                path = pathlib.Path(directory, name)
                if path.suffix in CODE_SUFFIXES and not name.endswith(".feature.cs"):
                    words.update(IDENTIFIER.findall(path.read_text(encoding="utf-8", errors="ignore")))
    return words


_line_counts = {}


def line_count(path):
    if path not in _line_counts:
        _line_counts[path] = len(path.read_text(encoding="utf-8", errors="ignore").splitlines())
    return _line_counts[path]


def path_exists(token, paths, by_name, directories):
    is_directory = token.endswith("/")
    token = token.strip("/")
    if token in paths or token in directories:
        return True
    if "/" not in token:
        # A bare name: a file of that name, or (written with a trailing slash) a folder of that name.
        return token in by_name or (is_directory and any(d.rsplit("/", 1)[-1] == token for d in directories))
    suffix = "/" + token
    return any(p.endswith(suffix) for p in paths) or any(d.endswith(suffix) for d in directories)


def cited_lines(text):
    numbers = []
    for part in re.split(r"\s*,\s*", text):
        bounds = [int(n) for n in re.split(r"\s*[-–]\s*", part)]
        numbers.append(max(bounds))
    return numbers


def looks_like_path(token):
    return "/" in token or token.endswith(FILE_SUFFIXES)


def is_route(token, paths, directories):
    """`/health`, `/api/…`, `/cancel`: an HTTP route, unless it names a repository folder (CODEOWNERS style)."""
    if not token.startswith("/"):
        return False
    stripped = token.strip("/")
    return stripped not in directories and stripped not in paths


def is_placeholder(token):
    return any(mark in token for mark in ("<", ">", "*", "{", "}", "…", "...", " ", "xxx", "xx.y", "=", "://")) \
        or token in EXAMPLES


def is_never_committed(token):
    """Build output and local secrets are git-ignored by design (bin/, obj/, TestResults/, .env)."""
    parts = [p for p in token.strip("/").split("/") if p]
    return any(p in SKIP_DIRS for p in parts) or (parts and parts[-1].startswith(".env"))


def looks_like_code_names(token):
    """`AddService/AddPatient` or `TestUser.Admin()/Doctor()`: code names joined by '/', not a path."""
    if token.endswith("/") or token.endswith(FILE_SUFFIXES):
        return False
    parts = [p for p in token.split("/") if p]
    return "(" in token or (len(parts) > 1 and all(p[:1].isupper() for p in parts))


def check_document(document, paths, by_name, directories, code_words):
    problems = []
    for number, line in enumerate(document.read_text(encoding="utf-8").splitlines(), start=1):
        last_file = None
        for token in TICKED.findall(line):
            token = token.strip()
            where = f"{document.relative_to(ROOT).as_posix()}:{number}"

            citation = LINE_CITATION.match(token)
            if citation and token not in EXAMPLES:
                name = citation.group("file") or last_file
                if name is None:
                    continue
                last_file = name
                candidates = [ROOT / p for p in paths if p == name or p.endswith("/" + name)] if "/" in name \
                    else by_name.get(name, [])
                if not candidates:
                    problems.append(f"{where}: `{token}` cites {name}, which does not exist")
                    continue
                longest = max(line_count(c) for c in candidates)
                too_far = [n for n in cited_lines(citation.group("lines")) if n > longest]
                if too_far:
                    problems.append(f"{where}: `{token}` cites line {too_far[0]}, but {name} has {longest} lines")
                continue

            if is_placeholder(token) or token in DELETED_ON_PURPOSE or is_never_committed(token) \
                    or is_route(token, paths, directories):
                continue

            if document in SYMBOL_DOCUMENTS and looks_like_code_names(token):
                names = IDENTIFIER.findall(re.sub(r"\([^)]*\)", "", token))
                missing = [n for n in names if n not in code_words]
                if missing:
                    problems.append(f"{where}: `{token}` names {', '.join(missing)}, which does not appear in the code")
                continue

            if looks_like_path(token):
                if not path_exists(token, paths, by_name, directories):
                    problems.append(f"{where}: `{token}` does not exist")
                elif "." in pathlib.PurePosixPath(token).name:
                    last_file = pathlib.PurePosixPath(token).name
                continue

            if document in SYMBOL_DOCUMENTS and CODE_NAME.match(token) and re.search(r"[a-z][A-Z]|[A-Z]{2}[a-z]", token):
                names = IDENTIFIER.findall(re.sub(r"\([^)]*\)", "", token))
                missing = [n for n in names if n not in code_words]
                if missing:
                    problems.append(f"{where}: `{token}` names {', '.join(missing)}, which does not appear in the code")
    return problems


def main():
    # Findings quote document text; a Windows console's default code page cannot print all of it.
    sys.stdout.reconfigure(encoding="utf-8")
    paths, by_name, directories = index_repository()
    code_words = index_code_words()
    problems = []
    for document in sorted(DOCUMENTS):
        if document.exists():
            problems.extend(check_document(document, paths, by_name, directories, code_words))

    print(f"Documents: {len(DOCUMENTS)}. Files in the repository: {len(paths)}.")
    if problems:
        print("\nFAILED: references that no longer resolve")
        print("\n".join(f"  {p}" for p in problems))
        return 1
    print("\nG4 (c) passed: every cited path, line and code name resolves.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
