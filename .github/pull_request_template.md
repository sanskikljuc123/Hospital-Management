<!-- Fields below are required by docs/qe-framework/QUALITY_GATES.md. Leave none blank; write "none" with a reason where it applies. -->

## What and why

<!-- What this change does and the requirement, risk or defect it addresses (G-PR-03). -->

Implements / fixes:

## Tests

<!-- Which behaviors and risks the new or changed tests protect. For a defect fix: link the run where the new test failed before the fix (G-TST-03). -->

- [ ] New and changed tests fail when the behavior they protect is broken (I checked, not assumed)
- [ ] State-changing behavior is asserted by reading the state back, including after refusals
- [ ] New or changed endpoints have rows in `backend/api.Tests/Security/RoleByEndpointMatrix.cs` (G-SEC-01)
- [ ] Endpoints that take an object identifier have object-level (BOLA) tests or a recorded exemption (G-SEC-02)

## Tests removed, skipped or weakened (G-PR-05)

<!-- "none", or for each: test, kind (removed / skipped / assertion loosened), what it protected, allowed reason, evidence. See docs/qe-framework/RECORD_TEMPLATES.md section 6.8. -->

none

## AI-produced content (G-PR-04)

<!-- "none", or for each contribution: files, tool or agent, what you changed after generation, what you verified yourself and how. See RECORD_TEMPLATES.md section 6.5. -->

none

## Security and data

- [ ] No secrets, tokens or real personal data in code, tests, logs or examples (G-SEC-04)
- [ ] Touches authorization code or `keycloak/` (needs two approvers other than the author, G-PR-02): yes / no

## Not covered

<!-- What you did not test or review, and why. -->
