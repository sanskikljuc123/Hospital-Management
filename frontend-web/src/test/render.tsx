import { ReactElement } from "react";
import { Provider } from "react-redux";
import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { store } from "../store/store";

/**
 * Renders a component the way the app does: inside the Redux store the pages and forms dispatch to directly.
 * Returns a user-event instance for realistic typing and clicking.
 */
export function renderWithStore(ui: ReactElement) {
  return { user: userEvent.setup(), ...render(<Provider store={store}>{ui}</Provider>) };
}

/** Runs `body` with the process in another time zone, as the browser of a user in that zone would be. */
export async function inTimeZone(timeZone: string, body: () => Promise<void>) {
  const previous = process.env.TZ;
  process.env.TZ = timeZone;
  try {
    await body();
  } finally {
    process.env.TZ = previous;
  }
}
