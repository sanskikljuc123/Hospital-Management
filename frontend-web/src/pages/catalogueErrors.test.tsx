import { describe, expect, it } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { API, server, services } from "../test/server";
import { renderWithStore } from "../test/render";
import Doctor from "./Doctor/Doctor";
import Patient from "./Patient/Patient";
import Service from "./Service/Service";

// A failed save or delete used to look like nothing happened: errors were stored but never shown, and the drawer
// closed before the API answered (UI_TEST_DESIGN D-6, R-062). The mocked errors are checked against the API
// contract (R-061): a ProblemDetails body where the API sends one, an empty 403 where it sends none.
const problem = (status: number, title: string, detail: string) =>
  HttpResponse.json({ type: "https://tools.ietf.org/html/rfc9110", title, status, detail }, {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });

describe("catalogue pages show the API's errors @R-061 @R-062", () => {
  it("shows why a service cannot be deleted and keeps its row (409)", async () => {
    server.use(
      http.delete(`${API}/service/1`, () =>
        problem(409, "Conflict", "The service is still used by patients or doctors")),
    );
    const { user } = renderWithStore(<Service />);

    const row = await screen.findByRole("row", { name: new RegExp(services[0].name) });
    await user.click(within(row).getByRole("button", { name: "Delete" }));
    await user.click(await screen.findByRole("button", { name: "OK" }));

    expect(await screen.findByText("The service is still used by patients or doctors")).toBeInTheDocument();
    expect(screen.getByRole("row", { name: new RegExp(services[0].name) })).toBeInTheDocument();
  });

  it("keeps the form open with the API's reason when a new doctor is refused (400)", async () => {
    server.use(
      http.get(`${API}/doctor`, () => HttpResponse.json([])),
      http.post(`${API}/doctor`, () => problem(400, "Bad Request", "One or more service IDs do not exist")),
    );
    const { user } = renderWithStore(<Doctor />);

    await fillNewDoctor(user);
    await user.click(screen.getByRole("button", { name: "Create Doctor" }));

    expect(await screen.findByText("One or more service IDs do not exist")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create Doctor" })).toBeVisible();
    expect(screen.getByLabelText("Doctor Last Name")).toHaveValue("Doctor");
  });

  it("confirms a saved doctor and closes the form (201)", async () => {
    server.use(
      http.get(`${API}/doctor`, () => HttpResponse.json([])),
      http.post(`${API}/doctor`, async ({ request }) =>
        HttpResponse.json({ id: 21, departmentId: null, ...((await request.json()) as object) }, { status: 201 })),
    );
    const { user } = renderWithStore(<Doctor />);

    await fillNewDoctor(user);
    await user.click(screen.getByRole("button", { name: "Create Doctor" }));

    expect(await screen.findByText("Doctor created")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole("button", { name: "Create Doctor" })).not.toBeInTheDocument());
  });

  it("says the user is not allowed when the list is refused with an empty 403 @R-064", async () => {
    server.use(http.get(`${API}/Patient`, () => new HttpResponse(null, { status: 403 })));

    renderWithStore(<Patient />);

    expect(await screen.findByText("You are not allowed to do this.")).toBeInTheDocument();
  });

  it("says the server cannot be reached when the request fails on the network @R-064", async () => {
    server.use(http.get(`${API}/doctor`, () => HttpResponse.error()));

    renderWithStore(<Doctor />);

    expect(await screen.findByText("The server could not be reached. Check your connection and try again."))
      .toBeInTheDocument();
  });
});

async function fillNewDoctor(user: ReturnType<typeof renderWithStore>["user"]) {
  await user.click(await screen.findByRole("button", { name: "Add a new Doctor" }));
  await user.type(screen.getByLabelText("Doctor First Name"), "New");
  await user.type(screen.getByLabelText("Doctor Last Name"), "Doctor");
  await user.type(screen.getByLabelText("Doctor Address"), "2 Test Street");
  await user.type(screen.getByLabelText("Doctor Birthday"), "1980-01-02{Enter}");
  await user.click(screen.getByLabelText("Select Service"));
  await user.click(await screen.findByTitle(services[0].name));
}
