import { Doctor } from "../../../types/Dctor";

export interface DoctorsProps {
  doctors: Doctor[];
  status: "idle" | "loading" | "failed";
  search?: string;
  handleFilterChange: (filter: string) => void;
  handleEditDoctor: (doctor: Doctor) => void;
  handleDeleteDoctor: (id: number) => void;
  handleViewDoctor: (doctor: Doctor) => void;
  handleCreateDoctor: () => void;
}
