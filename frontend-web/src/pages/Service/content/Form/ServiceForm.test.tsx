import { describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import ServiceForm from "./ServiceForm";
import { service } from "../../../../types/Service";
import { renderWithStore } from "../../../../test/render";

// The API's PUT replaces every field, so an edit that leaves out the department clears it (R-059, UI_TEST_DESIGN D-3).
const stored: service = { id: 5, name: "Test Service", description: "Before", departmentId: 2 };

describe("ServiceForm @R-059", () => {
  it("keeps the department when only the description is changed", async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithStore(<ServiceForm service={stored} onSubmit={onSubmit} />);

    await user.clear(screen.getByLabelText("Description"));
    await user.type(screen.getByLabelText("Description"), "After");
    await user.click(screen.getByRole("button", { name: "Update Service" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toEqual({ id: 5, name: "Test Service", description: "After", departmentId: 2 });
  });
});
