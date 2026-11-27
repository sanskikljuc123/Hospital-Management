---
name: evidence-rules
description: How a QE agent cites evidence, labels facts, assumptions and questions, and formats its output for the Hospital Management test project. Use in every qe-analyst, qe-test-designer and qe-reviewer task, and whenever writing a testing finding.
---

# Evidence rules

The test project follows "evidence over assumptions" (`docs/testing/README.md`). These rules say how an
agent proves what it says.

## 1. Sources

Read, in this order, only what the task needs:

1. The code under `backend/` (and `frontend-web/` when the task is about the UI).
2. `docs/testing/` — the single source of truth for strategy, risks, the security matrix and gates.
3. `docs/API_ENDPOINTS.md` and `ARCHITECTURE.md` for documented intent.
4. Input the person gave in the conversation (requirement text, diff, test output).
5. For `qe-reviewer` only: the output of a command it ran itself (README, AI rule 1), quoted with the exact
   command line and the lines that matter, so that anyone can run the same command and compare.

Nothing else counts as a source — not memory of other projects, not general best practice presented as
a fact about this code. If the code and a document disagree, report the disagreement as a **Question**;
do not choose one.

## 2. Labels

Every statement in the output carries one label:

| Label | Requirement |
|---|---|
| **Fact** | Cites a source: `path/file.cs:line` (or a line range), a document section, a test name, or a quoted line of test output. Someone else can open the citation and see the same thing. |
| **Assumption** | Not proven. Says what would prove or disprove it. Proposed for `docs/testing/RISK_REGISTER.md` §3 if it matters. |
| **Question** | Needs a decision from a person. Says which risk or test depends on the answer. |

A statement with no label is not used.

## 3. Hard rules

- **Never invent** a file, line, endpoint, role, test name or test result. If you did not read it, do
  not cite it.
- **Never propose weakening a test:** no retries, skips, longer timeouts, broader assertions, changed
  expected status codes to match current behavior, or deleting a failing test.
- **Never rate a risk as final.** Ratings are proposals; the test owner sets them.
- **Never approve** a change, a test or another agent's output. Say what you checked and what you found.
- **Never include secrets** or real personal data in output, even if you see them in a file — say that a
  secret is present and where.
- **Say what you did not cover.** Every output ends with what was out of reach or not read.

## 4. Output format

```markdown
## Task
<one line: what was asked, about which code or document>

## Sources read
- <path or document section>
- …

## Findings
| # | Label | Statement | Evidence |
|---|---|---|---|
| 1 | Fact | … | `backend/api/Controllers/X.cs:42` |
| 2 | Assumption | … | would be proven by … |
| 3 | Question | … | affects R-00x |

## Proposals
<what the agent proposes — risks, test cases, review comments — each pointing to finding numbers>

## Not covered
<what was not read or not checked, and why>
```

Agents may add sections their own definition requires, after **Findings** and before **Not covered**.
