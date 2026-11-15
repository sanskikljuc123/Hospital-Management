---
name: risk-model
description: How to identify, propose ratings for, and map to tests the risks of a Hospital Management feature, endpoint, change or finding, using the risk model in docs/testing/RISK_REGISTER.md. Use when asked what could go wrong, to assess or rate a risk, to write abuse cases, or to check risk coverage.
---

# Risk model — how to apply it

The model itself (Impact and Likelihood scales, Severity bands, STRIDE, statuses) is defined in
`docs/testing/RISK_REGISTER.md` §1. **Read that section first; do not use any other scale.**

## 1. Find risks

For the thing under analysis, go through each question. Every "yes" or "don't know" is a candidate risk.

| STRIDE | Question for this system |
|---|---|
| Spoofing | Can a caller act as another user or doctor — through the token, or through an ID in the request body (e.g. `DoctorId`, `PatientId`)? |
| Tampering | Can a caller change data they should not, or put data into an invalid state (status, time slot, links)? |
| Repudiation | Is the change written to the audit log (`AuditActions`), with who and what? |
| Information disclosure | Can a patient see another patient's data, or learn that an ID exists? Do errors leak internals? |
| Denial of service | Can one caller block others (e.g. hold every time slot)? Note it; load testing is out of scope. |
| Elevation of privilege | Can a role reach an action reserved for another role, including a user with no role or a deactivated account? |

Also check the business rules the code enforces (`ServiceResult.Invalid` / `Conflict` in `backend/Services/`):
each rule is a risk if it can be bypassed.

## 2. Check the register first

Search `docs/testing/RISK_REGISTER.md` §2 for an existing row. Reuse its ID instead of proposing a duplicate.
A new risk gets the label `new` — the test owner assigns the next free ID.

## 3. Propose a rating

For each risk give **Impact** and **Likelihood** with a one-line reason tied to evidence, then compute
Severity with the bands in §1 of the register. Write it as a proposal:

```markdown
| Risk | STRIDE | I | L | Severity | Reason | Evidence |
|---|---|---|---|---|---|---|
| A doctor attributes a record to another doctor | S, R | 3 | 3 | High (9) — proposed | Any doctor can send any `DoctorId` | `MedicalRecordService.cs:36,45` |
```

Medical data disclosure or account takeover is Impact 4. "Any authenticated user can do it by changing one
value in a request" is Likelihood 3 or 4.

## 4. Write abuse cases

For each Critical or High risk, one abuse case:

```markdown
**Abuse case (R-xxx):** As <identity from TEST_STRATEGY §5>, I <action with the exact request>,
so that <harm>. **Must result in:** <status and the state that must not change>.
```

## 5. Map to tests

For each risk, say which **level** (per `docs/testing/TEST_STRATEGY.md` §2) can prove it and what the
assertion is. Name existing tests that already carry `[Trait("Risk", "R-xxx")]`; otherwise say "none".
Leave test design detail to `qe-test-designer`.
