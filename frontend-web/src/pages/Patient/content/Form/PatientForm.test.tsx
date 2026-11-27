import { describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import PatientForm from "./PatientForm";
import { Patient } from "../../../../types/Patient";
import { renderWithStore } from "../../../../test/render";

// The API's PUT replaces every field, so an edit that leaves out the email erases it (R-059, UI_TEST_DESIGN D-2).
const stored: Patient = {
  id: 4,
  userName: "Test Patient",
  phone: "12345678",
  email: "test.patient@hospital.test",
  serviceId: 1,
};

describe("PatientForm @R-059", () => {
  it("keeps the email when only the name is changed", async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithStore(<PatientForm patient={stored} onSubmit={onSubmit} />);

    expect(screen.getByLabelText("Patient email")).toHaveValue("test.patient@hospital.test");
    await user.clear(screen.getByLabelText("Patient Name"));
    await user.type(screen.getByLabelText("Patient Name"), "Test Patient Renamed");
    await user.click(screen.getByRole("button", { name: "Update Patient" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    // The whole body, not a subset: the PUT replaces every field, so a missing phone would erase it too
    // (assertion rule 16).
    expect(onSubmit.mock.calls[0][0]).toEqual({
      id: 4,
      userName: "Test Patient Renamed",
      phone: "12345678",
      email: "test.patient@hospital.test",
      serviceId: 1,
    });
  });

  it("refuses an email address that is not valid", async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithStore(<PatientForm patient={stored} onSubmit={onSubmit} />);

    await user.clear(screen.getByLabelText("Patient email"));
    await user.type(screen.getByLabelText("Patient email"), "not-an-email");
    await user.click(screen.getByRole("button", { name: "Update Patient" }));

    expect(await screen.findByText("Please enter a valid email address")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
