import { service } from "./Service";

export type Patient = {
  id: number;
  userName: string;
  phone: string;
  email?: string;
  serviceId?: number;
  serviceName?: string;
  service?: service;
};
