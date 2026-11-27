import { describe, expect, it } from "vitest";
import { act, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { API, server, services } from "../../../../test/server";
import { renderWithStore } from "../../../../test/render";
import PatientDetails from "./PatientDetails";

// The details drawer showed the service from the store, which may still hold the one loaded for the patient opened
// before (R-063). Found by the lint rule react-hooks/rules-of-hooks: the hooks were inside an if, and the service
// was written into the patient prop. The second test fails on that code.
const withService = (id: number, serviceId: number) => ({
  id,
  userName: `Test Patient ${id}`,
  phone: "12345678",
  serviceId,
});

describe("patient details @R-063", () => {
  it("shows the service of the patient, loaded from the API", async () => {
    server.use(http.get(`${API}/service/1`, () => HttpResponse.json(services[0])));

    renderWithStore(<PatientDetails patient={withService(10, 1)} />);

    expect(await screen.findByText(services[0].name)).toBeInTheDocument();
  });

  it("never shows the service of the patient opened before while the next one loads", async () => {
    server.use(http.get(`${API}/service/2`, () => HttpResponse.json(services[1])));
    const { rerender } = renderWithStore(<PatientDetails patient={withService(11, 2)} />);
    expect(await screen.findByText(services[1].name)).toBeInTheDocument();

    // The answer for the next patient's service is held back, so the moment in between is visible.
    let answer: () => void = () => {};
    const held = new Promise<void>((resolve) => (answer = resolve));
    server.use(
      http.get(`${API}/service/1`, async () => {
        await held;
        return HttpResponse.json(services[0]);
      }),
    );
    rerender(<PatientDetails patient={withService(12, 1)} />);

    expect(screen.queryByText(services[1].name)).not.toBeInTheDocument();
    expect(screen.queryByText("Service details:")).not.toBeInTheDocument();
    await act(async () => answer());
    expect(await screen.findByText(services[0].name)).toBeInTheDocument();
  });

  it("opens patients with and without a service one after the other, and asks the API only for services", async () => {
    // Guards the hook order: the hooks run for every patient, with a service or without one.
    const { rerender } = renderWithStore(
      <PatientDetails patient={{ id: 13, userName: "Test Patient 13", phone: "12345678" }} />,
    );
    expect(screen.getByText("Test Patient 13")).toBeInTheDocument();

    server.use(http.get(`${API}/service/1`, () => HttpResponse.json(services[0])));
    rerender(<PatientDetails patient={withService(14, 1)} />);
    expect(await screen.findByText(services[0].name)).toBeInTheDocument();

    // No handler for another service request: one would fail the test (onUnhandledRequest: "error").
    rerender(<PatientDetails patient={{ id: 15, userName: "Test Patient 15", phone: "12345678" }} />);
    expect(screen.getByText("Test Patient 15")).toBeInTheDocument();
    expect(screen.queryByText("Service details:")).not.toBeInTheDocument();
  });

  it("shows a service that comes with the patient when the patient has no service id", () => {
    renderWithStore(
      <PatientDetails patient={{ id: 16, userName: "Test Patient 16", phone: "12345678", service: services[1] }} />,
    );

    expect(screen.getByText(services[1].name)).toBeInTheDocument();
  });
});
