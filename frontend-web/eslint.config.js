// Lint (G1, docs/testing/QUALITY_GATES.md §1): finds defects that compile and that the tests may not reach — a hook
// called conditionally or with missing dependencies (stale data shown after an edit), a floating promise whose error
// nobody sees, unused code. `npm run lint` runs it; CI fails on any error.
import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "reports", ".stryker-tmp", "node_modules"] },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: { ecmaVersion: 2022, globals: globals.browser },
    plugins: { "react-hooks": reactHooks },
    rules: {
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "error",
      // A leading underscore marks a parameter a callback must accept but does not use.
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
    },
  },
);
