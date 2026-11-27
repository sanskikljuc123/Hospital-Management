import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { copyFileSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { decide, isAllowed } from "./agent-bash-policy.mjs";

const hook = fileURLToPath(new URL("./restrict-agent-bash.mjs", import.meta.url));

/** Runs a hook file as Claude Code does: a separate process, the input on stdin. */
function runHook(agent, stdinText, hookFile = hook) {
  const args = agent === undefined ? [hookFile] : [hookFile, agent];
  return spawnSync(process.execPath, args, { input: stdinText, encoding: "utf8" });
}

const bash = (command, extra = {}) => JSON.stringify({ tool_name: "Bash", tool_input: { command }, ...extra });
const denied = (result) => JSON.parse(result.stdout).hookSpecificOutput.permissionDecision === "deny";

test("the reviewer may run the listed test and gate commands", () => {
  for (const command of [
    "npm --prefix frontend-web test",
    "dotnet test backend/backend.sln",
    "dotnet test backend/tests/HospitalManagement.ApiTests",
    "dotnet test backend/tests/HospitalManagement.UnitTests/HospitalManagement.UnitTests.csproj",
    "dotnet test backend/tests/HospitalManagement.ApiTests --filter FullyQualifiedName~OutboxDispatcherHostTests",
    "dotnet test e2e/HospitalManagement.E2E",
    "python3 -I scripts/check_test_gates.py",
    "py -3 -I scripts/check_ids.py",
    "python3 -I scripts/check_assertion_rules.py",
    "py -3 -I scripts/register_changes.py origin/master",
    "py -3 -I scripts/flaky_report.py repeat",
    "python3 -I -m unittest discover -s scripts/tests",
    "node --test .claude/hooks/restrict-agent-bash.test.mjs",
  ]) {
    assert.equal(isAllowed("qe-reviewer", command), true, command);
  }
});

test("unlisted commands and shell tricks are refused", () => {
  for (const command of [
    "git status",
    "git push origin HEAD:master",
    "git commit -m x",
    "bash scripts/verify.sh",
    "dotnet test backend/backend.sln && git push",
    "dotnet test backend/backend.sln; git push",
    "npm --prefix frontend-web test > results.txt",
    "dotnet test backend/backend.sln --filter FullyQualifiedName~X|Y",
    "dotnet test backend/backend.sln --filter 'FullyQualifiedName~X'",
    "py -3 -I scripts/check_ids.py $(git push)",
    "py -3 -I scripts/../../evil.py",
    "py -3 scripts/check_ids.py",
    "py -3 scripts/check_assertion_rules.py",
    "py -3 -I scripts/check_assertion_rules.py > out.txt",
    "py -3 -I scripts/flaky_report.py ../../secrets",
    "py -3 -I scripts/flaky_report.py repeat/../..",
    "py -3 -I scripts/flaky_report.py /etc",
    "dotnet test backend/backend.sln\ngit push",
    "npm --prefix frontend-web run build",
    "rm -rf backend",
    "",
  ]) {
    assert.equal(isAllowed("qe-reviewer", command), false, JSON.stringify(command));
  }
  assert.equal(isAllowed("qe-reviewer", undefined), false);
});

test("no other agent may run anything", () => {
  for (const agent of ["qe-analyst", "qe-test-designer", "Explore", "toString", undefined]) {
    assert.equal(isAllowed(agent, "py -3 -I scripts/register_changes.py origin/master"), false, String(agent));
    assert.equal(isAllowed(agent, "dotnet test backend/backend.sln"), false, String(agent));
  }
});

test("a listed command goes on to the session's permission rules; the hook never approves", () => {
  assert.equal(decide("qe-reviewer", JSON.parse(bash("dotnet test backend/backend.sln"))), null);
});

test("the hook refuses when another agent calls it", () => {
  const decision = decide(
    "qe-reviewer",
    JSON.parse(bash("dotnet test backend/backend.sln", { agent_type: "Explore" })),
  );
  assert.equal(decision.hookSpecificOutput.permissionDecision, "deny");
});

test("run as a process, the hook denies an unlisted command", () => {
  const result = runHook("qe-reviewer", bash("git push origin HEAD:master"));
  assert.equal(result.status, 0, result.stderr);
  assert.equal(denied(result), true);
});

test("run as a process, the hook stays silent for a listed command", () => {
  const result = runHook("qe-reviewer", bash("dotnet test backend/backend.sln", { agent_type: "qe-reviewer" }));
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, "");
});

test("run as a process, the hook blocks (exit 2) when it cannot read its input", () => {
  for (const input of ["", "not json"]) {
    const result = runHook("qe-reviewer", input);
    assert.equal(result.status, 2, JSON.stringify(input));
    assert.match(result.stderr, /Blocked/);
  }
});

test("run as a process, the hook denies input without a command", () => {
  for (const input of ["null", '"x"', "{}"]) {
    const result = runHook("qe-reviewer", input);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(denied(result), true, input);
  }
});

test("run as a process without an agent name, the hook denies", () => {
  const result = runHook(undefined, bash("dotnet test backend/backend.sln"));
  assert.equal(result.status, 0, result.stderr);
  assert.equal(denied(result), true);
});

test("run as a process, the hook blocks (exit 2) when its policy module cannot be loaded", () => {
  const folder = mkdtempSync(join(tmpdir(), "agent-hook-"));
  try {
    const lonelyHook = join(folder, "restrict-agent-bash.mjs");
    copyFileSync(hook, lonelyHook);
    const result = runHook("qe-reviewer", bash("git push origin HEAD:master"), lonelyHook);
    assert.equal(result.status, 2, result.stderr);
    assert.match(result.stderr, /Blocked/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});
