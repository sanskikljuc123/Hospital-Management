import { describe, expect, it } from "vitest";
import { API } from "./server";
import { checkMockedResponse, checkRequest } from "./contract";

// The contract check guards every component test (R-061), so it gets tests of its own: each rule must pass what the
// API accepts and report what it does not, in words a developer can act on.
const get = (path: string) => checkRequest(new Request(`${API}${path}`));
const send = (method: string, path: string, body: unknown) =>
  checkRequest(new Request(`${API}${path}`, {
    method,
    body: JSON.stringify(body),
    headers: { "Content-Type": "application/json" },
  }));
const mocked = (method: string, path: string, response: Response) =>
  checkMockedResponse(new Request(`${API}${path}`, { method }), response);

describe("API contract check: URL parameters @R-061", () => {
  it("accepts the query parameters an endpoint declares, with values of their types", async () => {
    expect(await get("/Appointment?patientId=5&doctorId=7&from=2027-03-01T09:00:00Z&to=2027-03-02")).toEqual([]);
  });

  it("compares query parameter names without case, as the API binds them", async () => {
    expect(await get("/AuditLog?page=2&pageSize=50&action=Doctor.Created")).toEqual([]);
  });

  it("reports a query parameter the endpoint does not have, which the API would silently ignore", async () => {
    expect(await get("/Appointment?patient=5")).toEqual([
      'GET /api/Appointment: the API has no query parameter "patient" (it accepts patientId, doctorId, from, to); the API ignores it',
    ]);
  });

  it("reports a query parameter on an endpoint that takes none", async () => {
    expect(await get("/Doctor?search=smith")).toEqual([
      'GET /api/Doctor: the API has no query parameter "search" (it accepts none); the API ignores it',
    ]);
  });

  it("reports a value of the wrong type", async () => {
    expect(await get("/Appointment?patientId=abc&from=yesterday")).toEqual([
      'GET /api/Appointment query parameter patientId="abc" should be a 32-bit integer',
      'GET /api/Appointment query parameter from="yesterday" should be an ISO 8601 date-time',
    ]);
  });

  it("reports an integer outside the 32-bit range the API binds", async () => {
    expect(await get("/MedicalRecord?patientId=99999999999")).toEqual([
      'GET /api/MedicalRecord query parameter patientId="99999999999" should be a 32-bit integer',
    ]);
  });

  it("reports a single-value query parameter sent twice", async () => {
    expect(await get("/MedicalRecord?patientId=1&patientId=2")).toEqual([
      'GET /api/MedicalRecord: query parameter "patientId" is sent more than once',
    ]);
  });

  it("checks the type of a path parameter", async () => {
    expect(await get("/doctor/9")).toEqual([]);
    expect(await get("/doctor/abc")).toEqual(['GET /api/Doctor/{id} path parameter id="abc" should be a 32-bit integer']);
  });
});

describe("API contract check: methods, bodies and mocked responses @R-061", () => {
  it("reports a method the API does not offer on a path", async () => {
    expect(await send("PATCH", "/doctor/9", {})).toEqual([
      "PATCH /api/doctor/9: the API offers GET, PUT, DELETE on /api/Doctor/{id}, not PATCH",
    ]);
  });

  it("reports a request body field of the wrong type", async () => {
    expect(await send("POST", "/Service", { name: 5 })).toEqual([
      "POST /api/Service request body: $.name should be a string, is 5",
    ]);
  });

  it("accepts a mocked ProblemDetails error where the API sends one", async () => {
    const problem = Response.json({ title: "Conflict", status: 409, detail: "Still used" }, { status: 409 });
    expect(await mocked("DELETE", "/service/1", problem)).toEqual([]);
  });

  it("reports a mocked error with a body where the API sends none (ownership 403)", async () => {
    const forbidden = Response.json({ detail: "not yours" }, { status: 403 });
    expect(await mocked("GET", "/Patient/1", forbidden)).toEqual([
      "GET /api/Patient/{id} 403 has no body in the API, the mock sends one",
    ]);
  });
});
