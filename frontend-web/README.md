# frontend-web

The web frontend of Hospital Management: React 18, TypeScript, Redux Toolkit, antd, Vite, signed in through Keycloak.

- **Run it:** the root [Readme](../Readme.md) → _Setting up the Frontend (Web)_.
- **Scripts:** `npm run dev`, `npm run build`, `npm run lint` (ESLint), `npm test` (component tests, Vitest),
  `npx stryker run --mutate <files>` (mutation testing; it changes files in place while it runs, so commit or stash
  first).
- **How it is tested:** [docs/testing/TEST_STRATEGY.md](../docs/testing/TEST_STRATEGY.md); the gates a change must
  pass: [docs/testing/QUALITY_GATES.md §1](../docs/testing/QUALITY_GATES.md#1-gates).
