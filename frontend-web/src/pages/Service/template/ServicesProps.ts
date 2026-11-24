import { service } from "../../../types/Service";

export interface ServicesProps {
  services: service[];
  status: "idle" | "loading" | "failed";
  search?: string;
  handleFilterChange: (filter: string) => void;
  handleEditService: (service: service) => void;
  handleDeleteService: (id: number) => void;
  handleViewService: (service: service) => void;
  handleCreateService: () => void;
}
