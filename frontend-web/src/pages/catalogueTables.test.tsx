import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Doctors from "./Doctor/template/Doctors";
import Patients from "./Patient/template/Patients";
import Services from "./Service/template/Services";

// Edit opens the form with the row it was clicked on. When a table kept only its visible columns, the form never
// got the email or the department, and the API's PUT then cleared them (R-059, UI_TEST_DESIGN D-2, D-3).
const noop = () => {};

async function clickEdit(rowText: string) {
  const row = screen.getByRole("row", { name: new RegExp(rowText) });
  await userEvent.setup().click(within(row).getByRole("button", { name: "Edit" }));
}

describe("catalogue tables @R-059", () => {
  it("passes the whole doctor record to Edit, including fields not shown", async () => {
    const doctor = {
      id: 1, firstName: "Test", lastName: "Doctor", birthday: "1980-01-02T00:00:00Z",
      address: "1 Test Street", departmentId: 3, serviceIds: [1],
    };
    const handleEditDoctor = vi.fn();
    render(
      <Doctors doctors={[doctor]} status='idle' handleEditDoctor={handleEditDoctor} handleFilterChange={noop}
        handleDeleteDoctor={noop} handleViewDoctor={noop} handleCreateDoctor={noop} />,
    );

    await clickEdit("1 Test Street");

    expect(handleEditDoctor).toHaveBeenCalledWith(doctor);
  });

  it("passes the whole patient record to Edit, including the email", async () => {
    const patient = {
      id: 2, userName: "Test Patient", phone: "12345678", email: "test.patient@hospital.test",
      serviceId: 1, serviceName: "Test Service A",
    };
    const handleEditPatient = vi.fn();
    render(
      <Patients patients={[patient]} status='idle' handleEditPatient={handleEditPatient} handleFilterChange={noop}
        handleDeletePatient={noop} handleViewPatient={noop} handleCreatePatient={noop} />,
    );

    await clickEdit("Test Patient");

    expect(handleEditPatient).toHaveBeenCalledWith(patient);
  });

  it("passes the whole service record to Edit, including the department", async () => {
    const service = { id: 3, name: "Test Service", description: "Synthetic", departmentId: 2 };
    const handleEditService = vi.fn();
    render(
      <Services services={[service]} status='idle' handleEditService={handleEditService} handleFilterChange={noop}
        handleDeleteService={noop} handleViewService={noop} handleCreateService={noop} />,
    );

    await clickEdit("Test Service");

    expect(handleEditService).toHaveBeenCalledWith(service);
  });
});
