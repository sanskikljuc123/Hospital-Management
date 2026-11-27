import "@testing-library/jest-dom/vitest";
import { createElement } from "react";
import { act, cleanup } from "@testing-library/react";
import { ConfigProvider, message } from "antd";
import { combineReducers } from "@reduxjs/toolkit";
import { reducer, store } from "../store/store";
import { afterAll, afterEach, beforeAll, expect, vi } from "vitest";
import { server } from "./server";
import { checkMockedResponse, checkRequest } from "./contract";

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

// No antd motion for toasts: jsdom never finishes the leave animation, so a destroyed toast would stay in the DOM.
ConfigProvider.config({
  holderRender: (children) => createElement(ConfigProvider, { theme: { token: { motion: false } } }, children),
});

// One store per test: the pages dispatch to the app's store singleton directly, and Vitest isolates files, not
// tests, so without this a test would start with the lists and status an earlier test loaded (R-071). The state
// taken here, before any test runs, is the slices' initial state; a reset action brings it back after each test.
// Not covered: a request still pending when a test ends can settle in the next one.
const RESET = "test/resetStore";
const initialState = store.getState();
const appReducer = combineReducers(reducer);
store.replaceReducer((state, action) => (action.type === RESET ? initialState : appReducer(state, action)));

// Every request must match a handler: an unexpected call (wrong method or path) fails the test instead of
// silently returning nothing.
beforeAll(() => server.listen({ onUnhandledRequest: "error" }));

// Every request the frontend sends and every response a test mocks must fit the API contract
// (contracts/openapi.json, R-061). A mismatch fails the test that caused it.
const contractChecks: Promise<string[]>[] = [];
server.events.on("request:start", ({ request }) => {
  contractChecks.push(checkRequest(request));
});
server.events.on("response:mocked", ({ request, response }) => {
  contractChecks.push(checkMockedResponse(request, response));
});

afterEach(async () => {
  const problems = (await Promise.all(contractChecks.splice(0))).flat();
  // antd's message toasts live in document.body, outside what cleanup() unmounts, and stay for seconds. Without
  // this, a test that expects a toast can pass on the toast an earlier test left behind.
  await act(async () => message.destroy());
  cleanup();
  // After cleanup(), so no mounted page re-renders with the reset state.
  store.dispatch({ type: RESET });
  server.resetHandlers();
  expect(problems, "The frontend or a test mock disagrees with the API contract (contracts/openapi.json)").toEqual([]);
});
afterAll(() => server.close());
