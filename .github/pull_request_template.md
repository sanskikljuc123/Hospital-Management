## What and why

<!-- One or two sentences. -->

## Risks and tests (docs/testing/QUALITY_GATES.md §2)

- **Risks this change touches** (IDs from docs/testing/RISK_REGISTER.md):
- **Flow scenarios** (IDs from docs/testing/BUSINESS_FLOWS.md):
- **Tests that prove them** (names):
- **Tests removed, skipped or loosened?** No / Yes — why:
- **New or changed lines without a test that fails when they break?** No / Yes — which (G9 checks services, the
  access policy and `frontend-web/src`; a `Stryker disable` or `eslint-disable` needs its reason here):
- **Known gaps closed?** (removed `Skip = "Known gap R-xxx"`): No / Yes — which:
- **Risk set to `Covered`?** No / Yes — **red evidence** for each: link to a CI run (or pasted output) where its
  test failed on the old or deliberately broken code, then passed with the change. No red evidence, no `Covered`.

## Security

- [ ] Touches `[Authorize]`, `backend/api/Auth/`, ownership checks or token validation → which security tests changed:
- [ ] New or changed endpoint → row added in `AuthorizationMatrix.cs` and SECURITY_TESTING §2
- [ ] Leakage checklist (BUSINESS_FLOWS §5) applied to changed responses, errors and logs
- [ ] No secrets, no real personal data (synthetic data rules, BUSINESS_FLOWS §4)
- [ ] No new CodeQL alert of severity high or above (G10), or each one explained here

## Documentation

- [ ] API_ENDPOINTS / ARCHITECTURE / testing docs updated, or not needed because:
