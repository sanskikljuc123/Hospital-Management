// Mutation testing of changed frontend code (G9, docs/testing/TEST_STRATEGY.md §11). On a pull request CI passes
// --mutate with the changed source files only, and scripts/mutation_summary.py fails the job when a mutant on a
// changed line goes undetected. The component tests (Vitest, jsdom, MSW) are the test suite.
/** @type {import('@stryker-mutator/api/core').PartialStrykerOptions} */
export default {
  testRunner: "vitest",
  vitest: { configFile: "vitest.config.ts" },
  coverageAnalysis: "perTest",
  mutate: ["src/**/*.{ts,tsx}", "!src/**/*.test.{ts,tsx}", "!src/test/**", "!src/**/*.d.ts"],
  // Texts and CSS class names are not rules, as in the backend ("ignore-mutations": ["string"]).
  mutator: { excludedMutations: ["StringLiteral"] },
  reporters: ["progress", "json", "html"],
  jsonReporter: { fileName: "reports/mutation/mutation.json" },
  htmlReporter: { fileName: "reports/mutation/mutation.html" },
  tempDirName: ".stryker-tmp",
  // In place, not in a sandbox copy of frontend-web: the tests read ../contracts/openapi.json, outside this folder.
  // Stryker restores every file when it ends; locally, commit or stash first.
  inPlace: true,
  concurrency: 2,
  // The gate is about changed lines (mutation_summary.py), not a file's score.
  thresholds: { high: 95, low: 90, break: null },
};
