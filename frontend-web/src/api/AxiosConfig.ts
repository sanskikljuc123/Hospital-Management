import axios from "axios";
import { getAccessToken } from "../auth/keycloak";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://localhost:5117/api",
});

// Every API call carries a fresh access token from Keycloak.
api.interceptors.request.use(async (config) => {
  const token = await getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export default api;
