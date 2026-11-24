import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderWithStore } from "../test/render";
import PrivateRoutes from "./PrivateRoutes";

// The header names the page of the current address; an address without a menu entry has no title. Before, an
// effect copied the title into state (react-hooks/exhaustive-deps); now it is derived on every render.
function renderAt(path: string) {
  renderWithStore(
    <MemoryRouter initialEntries={[path]}>
      <PrivateRoutes />
    </MemoryRouter>,
  );
  return screen.getByRole("heading", { level: 4 });
}

describe("page title in the header", () => {
  it("names the page of the address", () => {
    expect(renderAt("/home")).toHaveTextContent(/^Home Page$/);
  });

  it("has no title for an address without a menu entry", () => {
    expect(renderAt("/no-such-page")).toHaveTextContent(/^$/);
  });
});
