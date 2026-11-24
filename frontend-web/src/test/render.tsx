import { ReactElement, ReactNode } from "react";
import { Provider } from "react-redux";
import { ConfigProvider } from "antd";
import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { store } from "../store/store";

/**
 * Renders a component the way the app does: inside the Redux store the pages and forms dispatch to directly.
 * antd's animations are off, so a drawer that closes is gone at once instead of after an animation whose length
 * depends on how busy the machine is (no waiting on timing, TEST_STRATEGY §8).
 * Returns a user-event instance for realistic typing and clicking; `rerender` keeps the same providers.
 */
export function renderWithStore(ui: ReactElement) {
  return { user: userEvent.setup(), ...render(ui, { wrapper: AppProviders }) };
}

/** A wrapper, not part of the rendered element, so `rerender` keeps the store and the theme. */
function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ConfigProvider theme={{ token: { motion: false } }}>
      <Provider store={store}>{children}</Provider>
    </ConfigProvider>
  );
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
