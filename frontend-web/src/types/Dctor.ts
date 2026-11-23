export type Doctor = {
  id: number;
  firstName: string;
  lastName: string;
  birthday: string;
  address: string;
  departmentId?: number | null;
  serviceIds?: number[];
  // speciality: string;
  // location: string;
  // experience: number;
  // description: string;
  // available: boolean;
};
