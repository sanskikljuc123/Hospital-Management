// Which Bash commands qe-reviewer may run (docs/testing/README.md, AI rule 1). No other agent has Bash, so no
// other agent is listed: an unknown agent gets nothing. Pure functions only, so the tests can import them without
// running the hook.

const python = "(?:python3|py -3) -I";
// A plain value such as a commit or a results folder inside the repository: no leading slash, no "..".
const safeArgument = "(?!/)(?![^ ]*\\.\\.)[A-Za-z0-9_./:-]+";
const gateScripts =
  "(?:register_changes|mutation_summary|flaky_report|check_test_gates|check_gate_names|check_doc_references|check_ids)\\.py";
const testProject =
  "(?:backend/backend\\.sln|backend/tests/HospitalManagement\\.(?:Unit|Api)Tests(?:/HospitalManagement\\.(?:Unit|Api)Tests\\.csproj)?|e2e/HospitalManagement\\.E2E(?:/HospitalManagement\\.E2E\\.csproj)?)";
const testFilter = "(?: --filter (?:FullyQualifiedName|DisplayName)~[A-Za-z0-9_.]+)?";

const commandsByAgent = {
  "qe-reviewer": [
    /^npm --prefix frontend-web test$/,
    new RegExp(`^dotnet test ${testProject}${testFilter}$`),
    new RegExp(`^${python} scripts/${gateScripts}(?: ${safeArgument})*$`),
    new RegExp(`^${python} -m unittest discover -s scripts/tests$`),
    /^node --test \.claude\/hooks\/restrict-agent-bash\.test\.mjs$/,
  ],
};

// Anything that could chain, redirect, substitute or quote its way out of the patterns above.
const shellMetacharacters = /[\r\n;&|<>`$(){}'"\\*?[\]!#]/;

/** True only when the command is on the agent's list; unknown agents get nothing. */
export function isAllowed(agent, command) {
  if (typeof command !== "string" || shellMetacharacters.test(command)) return false;
  const allowed = Object.hasOwn(commandsByAgent, agent) ? commandsByAgent[agent] : [];
  return allowed.some((pattern) => pattern.test(command));
}

/**
 * The hook's answer for one PreToolUse input: `null` hands the command on to the session's own permission rules
 * (the hook only narrows what an agent may run, it never approves anything), otherwise a deny decision.
 * `agent` is the agent the hook was configured for (its frontmatter); when Claude Code also names the calling
 * agent (`agent_type`), both must agree.
 */
export function decide(agent, input) {
  const caller = input?.agent_type;
  if (caller !== undefined && caller !== agent) {
    return deny(`This hook belongs to ${agent}, but ${caller} called it.`);
  }
  return isAllowed(agent, input?.tool_input?.command) ? null : deny(`Bash command is not on the list for ${agent}.`);
}

const deny = (reason) => ({
  hookSpecificOutput: { hookEventName: "PreToolUse", permissionDecision: "deny", permissionDecisionReason: reason },
});
