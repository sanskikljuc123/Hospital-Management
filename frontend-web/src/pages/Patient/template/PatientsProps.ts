import { Patient } from "../../../types/Patient";

export interface PatientsProps {
  patients: Patient[];
  status: "idle" | "loading" | "failed";
  search?: string;
  handleFilterChange: (filter: string) => void;
  handleEditPatient: (patient: Patient) => void;
  handleDeletePatient: (id: number) => void;
  handleViewPatient: (patient: Patient) => void;
  handleCreatePatient: () => void;
}
