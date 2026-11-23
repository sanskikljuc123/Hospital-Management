import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { server } from "./server";

// No Keycloak in component tests: the API client gets a fixed token, and the user has the roles a test sets.
vi.mock("../auth/keycloak", () => ({
  HOSPITAL_ROLES: ["Admin", "Doctor", "Nurse", "Patient"],
  getAccessToken: async () => "component-test-token",
  getHospitalRoles: () => ["Admin"],
  getDisplayName: () => "Test User",
  initKeycloak: async () => true,
  logout: () => {},
  default: {},
}));

// jsdom lacks two browser APIs antd uses.
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
// jsdom implements getComputedStyle but not its pseudo-element argument, which antd passes.
const getComputedStyle = window.getComputedStyle.bind(window);
window.getComputedStyle = (element: Element) => getComputedStyle(element);

// Every request must match a handler: an unexpected call (wrong method or path) fails the test instead of
// silently returning nothing.
beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());
