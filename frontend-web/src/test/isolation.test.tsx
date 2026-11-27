import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { message } from "antd";
import { renderWithStore } from "./render";
import { services } from "./server";
import { reducer, store } from "../store/store";
import Service from "../pages/Service/Service";

// The state a fresh app starts with, built from each slice's own initial state, not from the store under test.
const freshState = () =>
  Object.fromEntries(Object.entries(reducer).map(([key, slice]) => [key, slice(undefined, { type: "test/probe" })]));

// Each test must start clean, whatever ran before it in the file (R-071). The pairs depend on order on purpose:
// the first leaves state behind, the second checks the shared setup removed it. `shuffle: false` keeps that order
// even in a shuffled run; this file is the one exception to TEST_STRATEGY §4 rule 5 and §8.
describe("component tests do not inherit state from earlier tests @R-071", { shuffle: false }, () => {
  it("loads the service list into the store", async () => {
    renderWithStore(<Service />);

    expect(await screen.findByRole("row", { name: new RegExp(services[0].name) })).toBeInTheDocument();
    expect(store.getState().service.services).toHaveLength(services.length);
  });

  it("starts the next test with a fresh store", () => {
    expect(store.getState()).toEqual(freshState());
  });

  it("shows a toast", async () => {
    message.error("Toast left by an earlier test");

    expect(await screen.findByText("Toast left by an earlier test")).toBeInTheDocument();
  });

  it("starts the next test without the earlier toast", () => {
    expect(screen.queryByText("Toast left by an earlier test")).not.toBeInTheDocument();
  });
});
