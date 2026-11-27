// PreToolUse hook for Bash in qe-reviewer's frontmatter: node restrict-agent-bash.mjs <agent name>.
// Reads the hook input from stdin and denies every command that is not on the agent's list (agent-bash-policy.mjs).
// Fails closed where it can: if the input cannot be read, the policy cannot be loaded or the check throws, it
// exits 2, which blocks the tool call. It cannot fail closed if `node` itself does not start; the session's
// permission rules are then the only check (docs/testing/README.md, AI rule 1).
// The policy is imported inside the `try`, so a missing or broken policy module also exits 2.
import { stdin, stdout, stderr, argv, exit } from "node:process";

try {
  const { decide } = await import("./agent-bash-policy.mjs");
  const chunks = [];
  for await (const chunk of stdin) chunks.push(chunk);
  const decision = decide(argv[2], JSON.parse(Buffer.concat(chunks).toString("utf8")));
  if (decision) stdout.write(JSON.stringify(decision));
} catch (error) {
  stderr.write(`Blocked: the agent Bash hook could not check this command (${error.message}).\n`);
  exit(2);
}
