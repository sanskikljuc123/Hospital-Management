import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react-swc";

// Component tests (TEST_STRATEGY §2, level "Component"): React components and Redux slices in jsdom, with the
// API mocked at the network boundary by MSW. No Keycloak, no backend, no browser.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["src/test/setup.ts"],
    // antd and the icon packs are thousands of modules. Pre-bundling them once, as Vite does for the dev server,
    // keeps every test file from transforming them again.
    deps: {
      optimizer: {
        client: {
          enabled: true,
          include: ["antd", "@ant-design/icons", "react-icons/hi", "react-icons/fa", "react-icons/fa6", "dayjs"],
        },
      },
    },
    // antd renders a lot of DOM; the default 5 s is tight on a busy CI runner. A slow test still fails.
    testTimeout: 15_000,
  },
});
