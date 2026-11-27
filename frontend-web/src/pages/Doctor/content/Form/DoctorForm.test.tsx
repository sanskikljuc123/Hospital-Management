import { describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import DoctorForm from "./DoctorForm";
import { Doctor } from "../../../../types/Dctor";
import { inTimeZone, renderWithStore } from "../../../../test/render";

// The API's PUT replaces every field, so an edit that sends a field back missing erases it (R-059). The birthday is
// a calendar date and must not move with the user's time zone (R-060). UI_TEST_DESIGN UI-DI-02, UI-BND-07.
const stored: Doctor = {
  id: 7,
  firstName: "Test",
  lastName: "Doctor",
  birthday: "1980-01-02T00:00:00Z",
  address: "1 Test Street",
  departmentId: 3,
  serviceIds: [1],
};

describe("DoctorForm @R-059 @R-060 @R-061", () => {
  it("sends back every field the API replaces when a doctor is saved unchanged", async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithStore(<DoctorForm doctor={stored} onSubmit={onSubmit} />);

    await user.click(screen.getByRole("button", { name: "Update Doctor" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toEqual({
      id: 7,
      firstName: "Test",
      lastName: "Doctor",
      birthday: "1980-01-02",
      address: "1 Test Street",
      departmentId: 3,
      serviceIds: [1],
    });
  });

  it.each(["Europe/Sarajevo", "America/New_York"])(
    "shows and sends the stored birthday as the same day in %s",
    async (timeZone) => {
      await inTimeZone(timeZone, async () => {
        const onSubmit = vi.fn();
        const { user } = renderWithStore(<DoctorForm doctor={stored} onSubmit={onSubmit} />);

        expect(screen.getByLabelText("Doctor Birthday")).toHaveValue("1980-01-02");
        await user.click(screen.getByRole("button", { name: "Update Doctor" }));

        await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
        expect(onSubmit.mock.calls[0][0].birthday).toBe("1980-01-02");
      });
    },
  );

  it("sends a new doctor's picked birthday as that day, not as a UTC timestamp", async () => {
    await inTimeZone("Europe/Sarajevo", async () => {
      const onSubmit = vi.fn();
      const { user } = renderWithStore(<DoctorForm onSubmit={onSubmit} />);

      await user.type(screen.getByLabelText("Doctor First Name"), "New");
      await user.type(screen.getByLabelText("Doctor Last Name"), "Doctor");
      await user.type(screen.getByLabelText("Doctor Address"), "2 Test Street");
      await user.type(screen.getByLabelText("Doctor Birthday"), "1980-01-02{Enter}");
      await user.click(screen.getByLabelText("Select Service"));
      await user.click(await screen.findByTitle("Test Service A"));
      await user.click(screen.getByRole("button", { name: "Create Doctor" }));

      await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
      expect(onSubmit.mock.calls[0][0]).toEqual({
        firstName: "New",
        lastName: "Doctor",
        address: "2 Test Street",
        birthday: "1980-01-02",
        serviceIds: [1],
      });
    });
  });

  it("does not submit a new doctor while required fields are empty", async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithStore(<DoctorForm onSubmit={onSubmit} />);

    await user.click(screen.getByRole("button", { name: "Create Doctor" }));

    expect(await screen.findByText("Please enter the doctor birthday")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
