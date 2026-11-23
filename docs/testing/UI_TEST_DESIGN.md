# UI Test Design — `frontend-web`

A risk-based design for testing the web frontend: what can go wrong for a user, which test would catch it,
at which level, and what is still unknown. It follows [TEST_STRATEGY.md](TEST_STRATEGY.md) and the
evidence rules (every statement is a **Fact**, an **Assumption** or a **Question**). Nothing here is
implemented yet. Every rating is a **proposal** for the test owner ([RISK_REGISTER.md](RISK_REGISTER.md) §1).

Written 2026-11-23 from the code at `frontend-web/src/` and from replaying the frontend's own requests
against a running stack (Aspire, sample users, synthetic data). Records created for that replay were deleted
again; their audit entries remain.

## 1. Task

Design UI tests for the Doctors, Patients and Services pages, sign-in, navigation and session of
`frontend-web`, so that defects, edge cases and regressions are found before they reach `master`. The tests
must cover happy paths and negative, boundary, state, concurrency, data-integrity, accessibility,
responsive, reliability and security cases.

## 2. Sources read

- All of `frontend-web/src/` (pages, forms, slices, auth, layout), `frontend-web/index.html`
- `PatientServiceImp.cs`, `DoctorServiceImp.cs`, `ServiceServiceImp.cs`, `PatientCreationDto.cs`, `DoctorCreationDto.cs`, `ServiceDto.cs`
- `e2e/HospitalManagement.E2E/` (features, `BrowserSession.cs`, `HospitalApi.cs`, `xunit.runner.json`)
- TEST_STRATEGY, RISK_REGISTER, BUSINESS_FLOWS (F-02, F-13), QUALITY_GATES §1, `docs/API_ENDPOINTS.md`
- A live run: every role signed in through Keycloak, 28 endpoints per role, plus the probes in §3

## 3. Defects found while designing

These were found by reading the code and confirmed by sending the frontend's exact requests to the API as
`admin.user`. Each has a test in §6 that fails until it is fixed.

**D-1 to D-5 are fixed** (2026-11-23, on the branch docs/ui-test-design). The fix was checked in a real browser
(Chromium, Playwright, signed in as `admin.user` through Keycloak) in the time zones Europe/Sarajevo and
America/New_York: edit doctor, patient and service, then compare with the API; create a doctor with a picked
birthday. 12 of 12 checks passed. A first run caught that D-2 and D-3 had a second cause: the tables copied only
their visible columns into each row, and Edit passed that trimmed row to the form. The checks are a script in
the session, not yet tests in `e2e/` (UI-HP-04 … 06, UI-BND-07 remain to be written).

| # | Label | Defect | Evidence |
|---|---|---|---|
| D-1 | Fact, fixed | **Editing a doctor never worked.** The frontend sent `PATCH /api/doctor/{id}`; the API only has `PUT`, so it answered 405 and the page showed nothing. Now `PUT` | `doctorSlice.ts:70`; probe before the fix: `PATCH /api/doctor/1` → 405 |
| D-2 | Fact, fixed | **Editing a patient erased their email.** The form had no email field, the table row passed to Edit had no email either, and `PUT` replaces every field. Now the form has an email field and the row keeps the whole record | `PatientForm.tsx:53`, `Patients.tsx:27`, `PatientServiceImp.cs:75`; probe before the fix: email `probe@hospital.local` → `null` |
| D-3 | Fact, fixed | **Editing a service erased its department.** Same two causes. Now the form sends the current department in a hidden field | `ServiceForm.tsx:32`, `Services.tsx:26`, `ServiceServiceImp.cs:52`; probe before the fix: `departmentId` 1 → `null` |
| D-4 | Fact, fixed | With `PUT`, **editing a doctor would have erased birthday and department**: the birthday field was hidden when editing and there was no department field. Now the birthday is shown and the department sent | `DoctorForm.tsx:43`, `:72`; `Doctors.tsx:27`; `DoctorServiceImp.cs:89, 91` |
| D-5 | Fact, fixed | **A doctor's birthday could be saved one day early.** The API keeps only the date of the UTC instant it receives (`1980-01-01T23:00:00.000Z` was stored as `1980-01-01`), and the picker sent local midnight as a UTC timestamp. Now the form sends `YYYY-MM-DD` and reads only the date part back | `DoctorForm.tsx:24`, `:29`; browser check: 1980-01-02 picked and stored in both zones |
| D-6 | Fact | **No failed request is ever shown to the user.** Errors are stored in the slices (`selectError`) but nothing reads them, and the drawer closes before the request finishes. 400, 403, 405, 409, 500 and network errors all look like "nothing happened" | `doctorSlice.ts:155`, `patientSlice.ts:156`, `serviceSlice.ts:187`; `Doctor.tsx:64-67` |
| D-7 | Fact | **Every role sees every action.** No page reads the user's roles; a doctor sees "Add a new Doctor", a patient sees an empty Patients page with no explanation | `PrivateRoutes.tsx:57-62`; role check in this session (patient: `GET /api/Patient` → 403) |
| D-8 | Fact | Doctor details show the **first name as the last name** | `DoctorDetails.tsx:15` |
| D-9 | Fact | The last-name column of the doctors table is titled "Phone Last name" | `Doctors.tsx:46` |
| D-10 | Fact | The **search boxes do nothing** (they only log to the console) | `Doctors.tsx:19` |
| D-11 | Fact | **A phone number with 10 or more digits cannot be saved** (the API field is an `int`); the form gives no hint and the 400 is not shown (D-6). Overlaps R-044 | `PatientCreationDto.cs:10`, `PatientForm.tsx:48`; probe: `38761123456` → 400 |
| D-12 | Fact | Form rules and API rules disagree: the form has no length limits (API: name 200, description 1000), and it requires a description the API does not | `ServiceDto.cs:12-15`; probes: 201-character name → 400, no description → 201 |
| D-13 | Fact | Patient details call React hooks inside an `if` and write into the `patient` prop | `PatientDetails.tsx:10-16` |
| D-14 | Fact | An unknown URL shows an empty page instead of "not found" | `PrivateRoutes.tsx:57-62` (no catch-all route) |
| D-15 | Fact | API calls have no timeout: a request that never answers leaves the page loading forever | `AxiosConfig.ts:4-6` |
| D-16 | Fact | The browser tab title is misspelled: "Hopital managment" | `index.html:8` |

## 4. Proposed risks

Existing risks reused where they fit. New ones are labelled `new` until the test owner gives them an ID.

| Risk | STRIDE | I | L | Severity (proposed) | Reason | Evidence |
|---|---|---|---|---|---|---|
| new — editing a patient, service or doctor in the UI silently erases fields the form does not show | T | 3 | 4 | **Critical (12)** | Happens on every normal edit; wrong data saved, nobody is told | D-2, D-3, D-4 |
| new — a failed write looks like a successful one, so users believe data was saved | T, R | 2 | 4 | **High (8)** | Every 400/403/409/5xx in normal use; misleading | D-6 |
| new — a date chosen in the UI is saved as a different date | T | 2 | 4 | **High (8)** | Every user east of UTC; the clinic's own time zone is UTC+1 | D-5 |
| new — a UI feature is broken for every user and nothing notices (no frontend tests in CI) | D | 3 | 3 | **High (9)** | D-1 shipped with every gate green; G1 only builds the frontend | D-1; QUALITY_GATES §1 G1 |
| new — the UI offers actions the role may not use | E | 1 | 4 | Medium (4) | The API refuses them (R-005), so no access is gained; users are confused | D-7 |
| new — work in a form is lost when the session ends | D | 1 | 2 | Low (2) | `getAccessToken` redirects to the login page without saving input | `keycloak.ts:30-38` |
| R-023 — XSS steals the access token | I, E | 4 | 1 | Medium (4) | Existing; still `Proposed`, no test | §6.10 |
| R-049 — the same person registered twice | T | 3 | 3 | High (9) | Existing; the UI adds a way: a double click on "Create Patient" (no disabled state) | §6.5 |
| R-044 — the patient model cannot hold real patients | T | 3 | 3 | High (9) | Existing; the UI shows it (D-11) | §6.3 |

**Abuse case (new, Critical):** As a nurse I open a patient whose email is on file, correct a typo in the
name and click "Update Patient", so that the patient's email is deleted and appointment notifications stop.
**Must result in:** the email is unchanged after the save; only the name changed.

**Abuse case (new, High, failed write):** As a doctor I click "Add a new Doctor", fill the form and submit,
so that I believe the doctor exists. **Must result in:** a visible message that I am not allowed (or no
such button at all), and no new row in the table.

## 5. Test levels and approach

Per TEST_STRATEGY §2, a check belongs at the lowest level that can prove it. The frontend has **no test
level below E2E today** (no test script in `frontend-web/package.json`), so this design proposes one:

| Level | Tool | Proves | Real dependencies | Status |
|---|---|---|---|---|
| **Component** (proposed) | Vitest + React Testing Library + MSW (mocked HTTP) | A form sends the right payload; an error response is shown; a role sees the right buttons; loading and empty states | none | **Question Q-a**: needs the test owner's decision and a CI job |
| **E2E** (exists) | Reqnroll + Playwright for .NET, `e2e/HospitalManagement.E2E` | A journey works through real Keycloak, API and database; data in the UI equals data in the API | Compose stack, production build | Add scenarios below |
| **E2E with fault injection** | Playwright `RouteAsync` on API calls | What the UI does when the API is slow, down or returns 500 | as E2E | New pattern; only for §6.9 |

Without the component level, every row marked **Comp** in §6 falls back to E2E, which is slower and does not
run on every pull request (G7).

## 6. Test catalogue

Columns: **Sev** is the proposed severity of the risk the test proves; **Lvl** is Comp (component, proposed)
or E2E; **Auto** is the automation priority (P1 first). Tests marked **fails today** reproduce a defect
from §3.

### 6.1 Happy paths — the main jobs work and are saved

Business value: the catalogue (doctors, services, patients) is the data every booking depends on.

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-HP-01 | High | Admin adds a doctor with services | Admin signed in; a service the test created → Doctors → Add → fill names, address, birthday, pick the service → Create | Row appears; after reload still there; `GET /api/Doctor/{id}` has the same values and service | Empty service dropdown (redirect/CORS), wrong payload, row only in local state | E2E | P1 |
| UI-HP-02 | High | A new service is offered in the doctor and patient forms without reload | Admin → Services → Add → open Doctors → Add | New service is an option | Stale cached service list | E2E | P2 |
| UI-HP-03 | High | Nurse registers a patient (F-02.1) | Nurse signed in → Patients → Add → name, phone, service → Create | Row appears; API record equal | Payload mismatch, 400 swallowed | E2E | P1 |
| UI-HP-04 | High | Admin edits a doctor's name — fixed (D-1), test still to write | Doctor created via API → Edit → change first name → Update | Table and API show the new name; birthday, department, services unchanged | 405 on PATCH; fields erased (D-4) | E2E | P1 |
| UI-HP-05 | Critical | Staff edits a patient's name — fixed (D-2), test still to write | Patient with email created via API → Edit → change name → Update | Name changed; **email, phone, service unchanged** in the API | Silent field erasure | E2E | P1 |
| UI-HP-06 | Critical | Admin edits a service description — fixed (D-3), test still to write | Service with department created via API → Edit → change description → Update | Description changed; **department unchanged** | Silent field erasure | E2E | P1 |
| UI-HP-07 | Medium | Admin deletes an unused service | Service created via API → Delete → confirm | Row gone; reload: still gone; API 404 | Row removed only locally | E2E | P2 |
| UI-HP-08 | Medium | Details drawer shows what the API holds — **fails today (D-8)** | Doctor, patient, service created via API → View each | Every shown value equals the API value | Wrong field bound | Comp | P2 |
| UI-HP-09 | Low | Navigation: every menu item opens its page | Signed in → click each menu item; open `/patient` directly | Right page, header title and highlighted item | Broken route, wrong title | E2E | P3 |

### 6.2 Negative — wrong input and forbidden actions are refused visibly

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-NEG-01 | Medium | Required fields empty | Any form → submit empty | Field messages; **no request sent** | Request sent with empty body | Comp | P2 |
| UI-NEG-02 | High | Delete a service still in use (R-019) — **fails today (D-6)** | Service used by a patient → Delete → confirm | Message "still used…"; row stays | 409 swallowed | E2E | P1 |
| UI-NEG-03 | High | Delete a patient who has appointments — **fails today (D-6)** | Patient with an appointment → Delete | Message; row stays | 409 swallowed | E2E | P2 |
| UI-NEG-04 | Medium | Doctor, nurse, patient cannot change doctors or services — **fails today (D-7)** | Each role → Doctors and Services pages | Add/Edit/Delete hidden (or disabled with a reason); forcing the request shows "not allowed" | Actions shown to wrong roles; 403 swallowed | Comp + E2E | P1 |
| UI-NEG-05 | Critical | A patient sees no other patient (R-003) | `patient.user` → Patients page | No patient data in the page **or in any network response**; an explanation instead of an empty table | Data leak through UI; confusing empty page | E2E | P1 |
| UI-NEG-06 | Medium | Nurse cannot delete a patient | Nurse → Patients | Delete hidden; forced request shows "not allowed"; row stays | Wrong role mapping in UI | Comp | P2 |
| UI-NEG-07 | Low | Unknown URL — **fails today (D-14)** | Signed in → `/does-not-exist` | "Page not found" with a link home | Blank page | Comp | P3 |
| UI-NEG-08 | Medium | Account without a hospital role (R-015) | Covered by `SignIn.feature` | "No access", token gets 403 | — | E2E | exists |
| UI-NEG-09 | High | API down when a page opens | API stopped / route aborted → open Doctors | Error state with "Try again"; **not** "Total Doctors number : 0" | Error shown as empty data | E2E fault | P2 |
| UI-NEG-10 | Medium | Keycloak unreachable at start | Keycloak URL unreachable → open app | "Sign-in is unavailable" with Try again (`AuthGate.tsx:40-48`) | Endless spinner | E2E fault | P3 |

### 6.3 Boundary — limits, empty values, special characters

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-BND-01 | Medium | Service name 200 vs 201 characters — **201 fails silently today (D-12)** | Service form | 200 saved; 201 blocked in the form with a message | UI and API limits disagree | Comp | P2 |
| UI-BND-02 | Medium | Doctor names 100/101, address 300/301 | Doctor form | Limit enforced in the form with the API's numbers | Same | Comp | P3 |
| UI-BND-03 | Medium | Patient name 200/201; email format | Patient form | Limit and email format checked in the form | Same; email cannot even be entered today | Comp | P3 |
| UI-BND-04 | High | Phone numbers: 8 digits, 11 digits with country code, leading zero, `1e5`, negative — **11 digits fails today (D-11)** | Patient form | Agreed format accepted; others refused with a message; value round-trips unchanged | Overflow, lost leading zero, number notation | Comp + E2E | P2 (after Q-c) |
| UI-BND-05 | Medium | Unicode round trip: `Đorđe Šćepanović`, `Zoë`, Arabic, emoji | Create doctor and patient with these names | Shown and stored exactly as typed | Encoding loss | E2E | P2 |
| UI-BND-06 | Medium | Whitespace-only names `"   "` | Any form | Refused in the form | antd `required` passes spaces; API 400 swallowed | Comp | P2 |
| UI-BND-07 | High | Birthday in UTC+1, UTC−5 and on a DST change — fixed (D-5), test still to write | Playwright context with `TimezoneId` → pick 2 Jan 1980 | API and table show 1980-01-02 in every zone | Off-by-one day | E2E | P1 |
| UI-BND-08 | Low | Empty lists | No services exist (fresh stack) | Empty-state text and working "Add" | Broken layout, wrong count | Comp | P3 |
| UI-BND-09 | Medium | 500 rows (R-037) | 500 services created via API → open page | Renders in < 2 s on CI hardware; scroll reaches the last row | Freeze; table with `pagination={false}` (`Doctors.tsx:121`) | E2E | P3 |
| UI-BND-10 | Low | Doctor with no service | Doctor form | Behavior per Q-d | Form and API disagree (form requires one, API allows none) | Comp | P3 |

### 6.4 State transitions — reload, navigation, session

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-ST-01 | Medium | Reload keeps the user signed in | Signed in on each page → reload | Same page, data reloaded, no login prompt | Session lost, double init | E2E | P2 |
| UI-ST-02 | Low | Back and forward keep the menu in step | Doctors → Patients → Back | Menu highlights Doctors | `defaultSelectedKeys` set only once (`SideMenu.tsx:36`) — *Assumption* | E2E | P3 |
| UI-ST-03 | Low | Two tabs | Tab A adds a service; tab B already open | Behavior per Q-f (stale until reload is acceptable if documented); no crash | Crash, duplicate rows | E2E | P3 |
| UI-ST-04 | High | Signed out, Back shows no data (F-13.3) | Partly in `SignIn.feature` → extend: after sign-out press Back | Login page; no table data in the DOM | Cached data after logout | E2E | P2 |
| UI-ST-05 | High | Work past the access-token lifetime (F-13.1) | Stack with a short `accessTokenLifespan` (test realm) → wait past it → save | Save succeeds; token refreshed silently | Expired token sent; 401 | E2E | P2 |
| UI-ST-06 | Low | Session ends while a form is filled (F-13.2) | Short SSO idle in a test realm → fill form → wait → submit | Per Q-e: input kept or a warning before redirect | Silent loss of input (`keycloak.ts:35`) | E2E | P3 |
| UI-ST-07 | Medium | Close a half-edited form and reopen | Edit → change field → close → Edit again | Original values, not the abandoned edit | Stale form state | Comp | P2 |
| UI-ST-08 | Medium | Create after Edit starts empty | Edit doctor A → close → Add | Empty form | Values from A leak into the new record | Comp | P2 |
| UI-ST-09 | Medium | Role removed in Keycloak while signed in (R-052) | Remove role via Keycloak admin → keep working | Documented behavior: access until token expiry (≤ 900 s, realm file) | Access longer than the token lifetime | E2E | P3 |

### 6.5 Concurrency — double clicks and parallel edits

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-CON-01 | High | Double-click "Create Patient" (R-049) | Fill form → double-click submit | Exactly one patient in the API; button disabled while saving | Duplicate records | E2E | P1 |
| UI-CON-02 | Medium | Two deletes in quick succession | Two unused services → delete both quickly | Both gone; table equals API | Lost update in list state | E2E | P3 |
| UI-CON-03 | Medium | Two admins edit the same service | Two contexts load the service → both save different descriptions | Per Q-f: conflict shown, or last write wins as documented | Silent overwrite (no concurrency token, ARCHITECTURE §9) | E2E | P3 |
| UI-CON-04 | Low | Leave the page before a save answers | Slow API (fault injection) → save → navigate away → return | No crash; saved record shown | Update to unmounted state | E2E fault | P3 |
| UI-CON-05 | Low | List and details share one loading flag | Open patient details while the Services list loads | Each part shows its own loading state | Shared `status` in `serviceSlice.ts` | Comp | P3 |

### 6.6 Data integrity — the UI shows what is stored

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-DI-01 | High | After every create, edit and delete, the table equals the API | Each CRUD step in §6.1 → compare table rows with `GET` | Same ids and values | Local state drift | E2E | P1 (part of §6.1) |
| UI-DI-02 | Critical | **Edit forms send every field `PUT` replaces** — catches D-2, D-3, D-4 as a class | Render each edit form with a full record → submit unchanged | Payload contains every DTO field with the original values | Field erasure on edit | Comp | P1 |
| UI-DI-03 | Medium | A failed write leaves the row unchanged | Mock 409/500 on delete and update | Row and values unchanged; message shown | Optimistic change not rolled back | Comp | P2 |
| UI-DI-04 | Low | Renamed service shows its new name on patients after reload | Rename via UI → Patients | New `serviceName` | Stale denormalized name | E2E | P3 |
| UI-DI-05 | Medium | A UI change is audited with the signed-in user (R-013) | Admin edits a service in the UI → `GET /api/AuditLog` | `Service.Updated` by `admin.user` | Writes outside the audited path | E2E | P3 |

### 6.7 Accessibility — keyboard, screen reader, contrast

Target standard is not defined (Q-g); WCAG 2.1 AA is assumed below. Automated scans use axe-core through the Playwright
page (the `Deque.AxeCore.Playwright` package for .NET).

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-A11Y-01 | Medium | axe scan of every page and every open drawer | Signed in as admin | No `serious` or `critical` violations | Missing labels, roles, contrast | E2E | P2 |
| UI-A11Y-02 | Medium | Icon-only View, Edit, Delete buttons have names | Tables | Each button has an accessible name ("Edit Dr. …") | *Assumption:* the Tooltip alone gives none | E2E | P2 |
| UI-A11Y-03 | Medium | Keyboard only: add a patient | Tab through menu → Add → fill → submit → Esc | Every step reachable; focus returns to "Add" when the drawer closes | Keyboard traps, lost focus | E2E | P2 |
| UI-A11Y-04 | Low | Delete confirmation by keyboard | Focus Delete → Enter → confirm with keyboard | Works; focus moves sensibly | Popconfirm unreachable | E2E | P3 |
| UI-A11Y-05 | Medium | Form errors are announced and linked to fields | Submit empty form | Errors have `aria-describedby` / live region | Silent errors for screen readers | Comp | P3 |
| UI-A11Y-06 | Low | Visible focus and contrast | Tab through each page | Visible focus ring everywhere; text contrast ≥ 4.5:1 | Low-contrast header and avatar colors | E2E | P3 |
| UI-A11Y-07 | Low | Headings and page title — **title fails today (D-16)** | Each page | One `h1` naming the page; tab title names page and app | `h4` page title, misspelled static title | Comp | P3 |

### 6.8 Responsive and cross-browser

TEST_STRATEGY §1 puts visual and layout testing out of scope; these are proposed only if Q-g changes that.

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-RSP-01 | Low | 1280×800 and 1920×1080 | Each page and drawer | No horizontal page scroll; table and actions usable | Clipped actions column | E2E | P3 |
| UI-RSP-02 | Low | Tablet 768×1024 portrait and landscape | Open each form | Drawer wide enough to use (it is 40% wide: `DrawerComponent.tsx:8`) | Unusable narrow form | E2E | P3 |
| UI-RSP-03 | Low | 200% zoom (WCAG reflow) | Each page | Content reflows, nothing hidden | Fixed heights (`scroll={{ y: 380 }}`) | E2E | P3 |
| UI-RSP-04 | Medium | Firefox and WebKit smoke | Sign in + UI-HP-03 | Pass in all three engines | Engine-specific breakage (date picker, PKCE) | E2E | P3 |

### 6.9 Reliability — slow, failing and partial backends

All use Playwright route interception on API requests; the stack itself stays real.

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-REL-01 | High | List request returns 500 | Fault on `GET /api/Doctor` | Error state with retry; no "0 doctors" | Error shown as empty data (D-6) | E2E fault | P2 |
| UI-REL-02 | Medium | Slow API (5 s) | Delay all API calls | Loading state; submit disabled; one request only | Double submit, no feedback | E2E fault | P2 |
| UI-REL-03 | High | Network drops during save | Abort `POST` | Message; form stays open with the entered values | Data lost, drawer already closed (`Doctor.tsx:67`) | E2E fault | P2 |
| UI-REL-04 | Medium | Request never answers — **fails today (D-15)** | Hang `GET /api/Service` | Timeout message after an agreed time | Spinner forever | E2E fault | P3 |
| UI-REL-05 | High | Services list fails while the doctor form is open — the defect class seen on 2026-11-23 | Fault on `GET /api/Service` → open Add doctor | Dropdown shows an error, submit blocked with a reason | Empty dropdown, no explanation | Comp + E2E | P1 |
| UI-REL-06 | Medium | Token refresh fails mid-session | Keycloak token endpoint aborted → save | Clear path back to sign-in; no loop | Redirect loop, silent failure | E2E fault | P3 |
| UI-REL-07 | High | Every way of starting the stack serves the API without a redirect | Compose and AppHost → `GET {VITE_API_URL}` from the browser origin | 2xx, CORS allowed, **no 3xx** | The https-profile redirect fixed in #49 | E2E smoke | P2 |

### 6.10 Security in the UI

| ID | Sev | Test | Preconditions → Steps | Expected | Failure modes detected | Lvl | Auto |
|---|---|---|---|---|---|---|---|
| UI-SEC-01 | Medium | Stored XSS (R-023) | Create service, doctor, patient with `<img src=x onerror=alert(1)>` and `<script>` in every text field → view list and details | Shown as text; no dialog, no extra request | Script execution, token theft | E2E | P1 |
| UI-SEC-02 | Medium | Token not reachable from storage (R-023) | Signed in → read `localStorage`, `sessionStorage`, `document.cookie` | No access or refresh token | Token persisted where XSS can read it | E2E | P2 |
| UI-SEC-03 | High | Visible actions match the role matrix (D-7) | Each of the four roles → each page | Only actions in `docs/API_ENDPOINTS.md` → *Role matrix* | Wrong role mapping | Comp | P1 |
| UI-SEC-04 | Critical | A patient's browser never receives another patient's data (R-001–R-003) | Linked patient → every page | No foreign patient id or name in any response body | Leak through a list endpoint | E2E | P1 |
| UI-SEC-05 | Medium | Sign-in redirect cannot be bent to another site | Auth URL with `redirect_uri=https://example.org/` | Keycloak refuses ("Invalid redirect uri") | Open redirect via realm config | E2E | P3 |
| UI-SEC-06 | Low | No request leaves the known origins | Record all requests during a full journey | Only frontend, API, Keycloak | Third-party calls (the unused `usersSlice.ts:20` points at a public placeholder API) | E2E | P3 |
| UI-SEC-07 | Low | Nothing sensitive in the console | Full journey | No token, patient data or config in console output (`App.tsx:4` logs config today) | Leaks via logs | E2E | P3 |
| UI-SEC-08 | Low | CSRF does not apply | Request with cookies but no `Authorization` | 401 | Cookie-based auth added by mistake | E2E | P3 |

## 7. Gaps and missing requirements

| # | Label | Gap | Affects |
|---|---|---|---|
| Q-a | Question | Add a **component test level** for `frontend-web` (Vitest + Testing Library + MSW) and run it on every PR? | Every **Comp** row; the "no frontend tests" risk |
| Q-b | Question | How must a refused or failed action look: hidden button, disabled button with a reason, a message after the click? | UI-NEG-02 … 06, UI-SEC-03 |
| Q-c | Question | What is a valid phone number (format, length, country code)? Today it is an `int` (R-044) | UI-BND-04 |
| Q-d | Question | May a doctor have no services, and is a service description required? The form and the API disagree (D-12) | UI-BND-10, UI-BND-01 |
| Q-e | Question | When the session ends during editing, must input be kept? | UI-ST-06 |
| Q-f | Question | Two people editing the same record: must the second see a conflict? Must other tabs refresh? | UI-CON-03, UI-ST-03 |
| Q-g | Question | Which accessibility standard, which browsers and which screen sizes are supported? TEST_STRATEGY §1 excludes layout testing today | §6.7, §6.8 |
| Q-h | Question | What should the search box search, and the Home page show? Both are empty today (D-10) | — |
| 1 | Fact | No acceptance criteria exist for any UI page; BUSINESS_FLOWS marks F-02 "UI ✅" although editing a patient loses data (D-2) | BUSINESS_FLOWS F-02 |
| 2 | Fact | Appointments, medical records, departments, users and audit have no pages; those flows can only be tested through the API | F-03 … F-14 |
| 3 | Fact | No error, loading-timeout or empty-state designs exist (D-6, D-15) | §6.9 |
| 4 | Fact | No `data-testid` or accessible names on table actions; locators depend on CSS classes and visible text | §8 |

## 8. Automation reliability

**Risks in this codebase**

| Risk | Where | Mitigation |
|---|---|---|
| CSS-class and text locators | `BrowserSession.cs:44` (`.header--user-name`), `:46` ("Logout") | Locate by role and accessible name first; add `data-testid` only where no role fits (table rows, action buttons) |
| antd renders dropdowns, popconfirms and drawers in portals at `body`, and virtualizes long option lists | Select, Popconfirm, Drawer | Open the control, then locate the option by role `option` and name; type to filter instead of scrolling |
| Drawer animation and `destroyOnClose` | `DrawerComponent.tsx:11` | Wait until the drawer is visible and its form is enabled; never wait a fixed time |
| A save is fire-and-forget and the drawer closes at once | `Doctor.tsx:64-67` | Assert on the network response (`RunAndWaitForResponseAsync`) and then on the table, not on the drawer closing |
| Loading spinners on tables | `Doctors.tsx:116` | Wait for the list response, then the row |
| One shared stack and shared sample users, scenarios run in sequence | `xunit.runner.json` | Each scenario creates its own records with a unique prefix and asserts only on them; never on totals or seed rows (`Service1`) |
| Time-zone-dependent dates | D-5 | Set `TimezoneId` on the browser context per scenario |
| Aspire is not covered by any test | TEST_STRATEGY §7 | UI-REL-07 as a smoke check of both start-up paths |

**Strategy**

- **Locators:** `GetByRole` with accessible names → `GetByLabel` for form fields → `data-testid` for table
  rows (`onRow` can add it) and icon buttons. No CSS classes, no XPath, no indexes.
- **Page objects:** one per page and per drawer (Doctors page, Doctor form, Patients page, …) next to
  `BrowserSession.cs`; steps call page objects, page objects never assert business rules.
- **Isolation:** data created through the API as admin (`HospitalApi.cs`) with a unique suffix; scenario
  clean-up deletes what it created where the API allows it.
- **Retries:** none (TEST_STRATEGY §8). A flaky test follows QUALITY_GATES §3.
- **Mocking:** only at the component level (MSW) and in the fault-injection rows (§6.9). Happy paths and
  security rows always use the real stack.
- **Data:** synthetic only (BUSINESS_FLOWS §4); no seed rows relied on.
- **CI:** component tests on every PR once Q-a is decided; the new E2E scenarios under G7. Playwright trace
  on failure is already collected.

## 9. Coverage matrices

### A. Risk coverage

| Risk | Tests | Lvl | Today |
|---|---|---|---|
| new — field erasure on edit (Critical) | UI-DI-02, UI-HP-04, UI-HP-05, UI-HP-06 | Comp, E2E | fixed; no test yet |
| new — failed write looks successful (High) | UI-NEG-02, UI-NEG-03, UI-NEG-09, UI-DI-03, UI-REL-01, UI-REL-03 | Comp, E2E | **fails** |
| new — date saved as a different date (High) | UI-BND-07 | E2E | fixed; no test yet |
| new — broken UI feature unnoticed (High) | everything in §6; specifically UI-HP-04 | Comp, E2E | **fails** |
| new — actions offered to the wrong role (Medium) | UI-NEG-04, UI-NEG-06, UI-SEC-03 | Comp | **fails** |
| new — input lost when the session ends (Low) | UI-ST-06 | E2E | unknown (Q-e) |
| R-001, R-002, R-003 | UI-NEG-05, UI-SEC-04 | E2E | expected to pass (API enforces) |
| R-015 | UI-NEG-08 (`SignIn.feature`) | E2E | passes |
| R-019 | UI-NEG-02, UI-NEG-03 | E2E | API passes, UI message missing |
| R-023 | UI-SEC-01, UI-SEC-02 | E2E | not tested |
| R-037 | UI-BND-09 | E2E | not tested |
| R-044 | UI-BND-04 | Comp, E2E | **fails** (D-11) |
| R-049 | UI-CON-01 | E2E | not tested |
| R-052 | UI-ST-09 | E2E | not tested |

### B. Edge-case coverage by page

| Category | Sign-in / shell | Doctors | Patients | Services |
|---|---|---|---|---|
| Happy path | exists | HP-01, 04, 08 | HP-03, 05, 08 | HP-02, 06, 07, 08 |
| Negative | NEG-07, 08, 10 | NEG-01, 04 | NEG-01, 03, 05, 06 | NEG-01, 02, 04 |
| Boundary | – | BND-02, 05, 07, 10 | BND-03, 04, 05, 06 | BND-01, 08, 09 |
| State | ST-01, 02, 04, 05, 06, 09 | ST-07, 08 | ST-07 | ST-03 |
| Concurrency | – | – | CON-01 | CON-02, 03, 05 |
| Data integrity | – | DI-01, 02 | DI-01, 02, 04 | DI-01, 02, 03, 05 |
| Accessibility | A11Y-01, 06, 07 | A11Y-02, 04 | A11Y-03, 05 | A11Y-02 |
| Responsive | RSP-03, 04 | RSP-01, 02 | RSP-02 | RSP-01 |
| Reliability | REL-06, 07 | REL-01, 04 | REL-03 | REL-02, 05 |
| Security | SEC-02, 05, 06, 07, 08 | SEC-01, 03 | SEC-01, 04 | SEC-01, 03 |

### C. Missing test areas

- No frontend unit or component tests at all (Q-a).
- No UI for appointments and medical records, so the clinical flows have no UI tests (only API through E2E).
- No performance budget for pages; only UI-BND-09 touches it.
- No test of the Aspire start-up path (UI-REL-07 proposes one).

### D. Defect leakage risks

1. **Silent data loss on edit** (D-2, D-3, D-4): users will not notice until a notification fails or a
   report is wrong. Highest leakage risk, because nothing in the UI hints at it.
2. **Silent failures** (D-6): every future API rule (409, 400) becomes invisible in the UI.
3. **Time zones** (D-5): passes on a CI machine in UTC, fails for every user in the clinic's zone. UI-BND-07
   must force the zone.
4. **Environment wiring** (redirects, CORS, base URLs): not visible to unit or API tests; the 2026-11-23
   redirect shows it happens.
5. **Role display** (D-7): low harm, but every new page will copy the pattern.

### E. Recommended additional tests (outside the UI)

| Test | Level | Why |
|---|---|---|
| `PATCH` on any `/api/*` resource returns 405, and the frontend uses no method the API lacks (compare `api.*` calls in `frontend-web/src` with the controllers) | static check in CI | Would have caught D-1 without a browser |
| `PUT` contract: every update DTO field is documented as "replaced" in `docs/API_ENDPOINTS.md`, so the UI knows to send it | API | Makes D-2 … D-4 a documented contract |
| `Birthday` keeps the calendar date of an offset time (`1980-01-02T00:00:00+01:00` → `1980-01-02`) | API | Fixes D-5 at the source; the UI test then confirms it |

### F. Confidence assessment

| State | Confidence that a UI defect is caught before `master` | Why |
|---|---|---|
| Today | **~15 %** | Only sign-in runs in a browser; D-1 … D-16 all exist with every gate green |
| After the P1 tests (14 tests) | **~65 %** | Data loss, silent failures, role display, time zone, XSS, patient leakage and today's environment failure are covered |
| After all P1–P2 tests and the component level (Q-a) | **~85 %** | Remaining gaps: layout, browsers other than Chromium, performance, pages that do not exist yet |

These percentages are judgement, not measurement (*Assumption*); mutation testing of the frontend would be
the way to measure them.

## 10. Not covered

- The UI was not clicked through in a browser for this design; the defects were confirmed by sending the
  frontend's exact requests to the API. D-5 (date picker) and the accessibility assumptions (UI-A11Y-02)
  still need a browser run.
- Keycloak's own login page (theme, accessibility) is out of scope (TEST_STRATEGY §1).
- `npm audit` and dependency risks are covered by G1, not here.
- The planned pages for appointments and medical records do not exist and are not designed here.
