import { describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { API, server } from "../../test/server";
import { store } from "../../store/store";
import { updateDoctor } from "./doctorSlice";

// The API offers PUT for a doctor, not PATCH; editing a doctor sent PATCH and always got 405 (R-059,
// UI_TEST_DESIGN D-1). The server here answers only PUT, and any other request fails the test (setup.ts).
describe("doctorSlice @R-059", () => {
  it("saves an edited doctor with PUT to the doctor's address, with every field in the body", async () => {
    const doctor = {
      id: 9, firstName: "Test", lastName: "Doctor", birthday: "1980-01-02", address: "1 Test Street",
      departmentId: 3, serviceIds: [1],
    };
    let received: { method: string; body: unknown } | undefined;
    server.use(
      http.put(`${API}/doctor/9`, async ({ request }) => {
        received = { method: request.method, body: await request.json() };
        return HttpResponse.json(doctor);
      }),
    );

    const result = await store.dispatch(updateDoctor(doctor));

    expect(result.meta.requestStatus).toBe("fulfilled");
    expect(received).toEqual({
      method: "PUT",
      body: {
        firstName: "Test", lastName: "Doctor", birthday: "1980-01-02", address: "1 Test Street",
        departmentId: 3, serviceIds: [1],
      },
    });
  });
});
