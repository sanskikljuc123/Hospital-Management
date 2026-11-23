import { Doctor } from "./Dctor";
import { Patient } from "./Patient";

export type service = {
  id: number;
  name: string;
  description: string;
  departmentId?: number | null;
  patients?: Patient;
  doctor?: Doctor[];
  // speciality: string;
};
