import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";

/** The API base URL the frontend uses when VITE_API_URL is not set (`AxiosConfig.ts`). */
export const API = "http://localhost:5117/api";

/** Synthetic services every form test can choose from (BUSINESS_FLOWS §4). */
export const services = [
  { id: 1, name: "Test Service A", description: "Synthetic", departmentId: 1 },
  { id: 2, name: "Test Service B", description: "Synthetic", departmentId: 2 },
];

/** The handlers every test gets; a test adds its own with `server.use(...)`. */
export const server = setupServer(http.get(`${API}/Service`, () => HttpResponse.json(services)));
