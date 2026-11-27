"""R-075: the advisory AI review must never be able to post a secret. Run: python3 -m unittest discover -s scripts/tests

The workflow cannot be unit-tested by running it, so these tests read .github/workflows/assertion-review.yml and keep
the properties that prevent a leak from being edited away: the agent job has a read-only token and cannot post; the
agent may only read the checkout and run `gh pr diff`; reading outside the checkout is denied; and the posting job,
which has no AI in it, refuses text shaped like a secret. Text checks, so no YAML library is needed.
"""
import pathlib
import re
import unittest

WORKFLOW = pathlib.Path(__file__).resolve().parents[2] / ".github" / "workflows" / "assertion-review.yml"
TEXT = WORKFLOW.read_text(encoding="utf-8")


def job(name):
    """The text of one job: from '  <name>:' to the next job or the end."""
    match = re.search(rf"^  {name}:\n(.*?)(?=^  [a-z_-]+:\n|\Z)", TEXT, re.M | re.S)
    assert match, f"job {name} not found"
    return match.group(1)


def permissions(job_text):
    match = re.search(r"^    permissions:\n((?:      .*\n)+)", job_text, re.M)
    assert match, "job has no permissions block"
    return sorted(line.strip() for line in match.group(1).splitlines())


def tool_list(flag):
    match = re.search(rf'--{flag} "([^"]*)"', TEXT)
    assert match, f"--{flag} not found"
    return match.group(1).split(",")


def secret_pattern():
    match = re.search(r"grep -qE '([^']+)' review\.md", TEXT)
    assert match, "the posting job has no secret pattern check"
    return match.group(1)


class AgentJobCannotPostOrWrite(unittest.TestCase):
    def test_workflow_default_is_no_permissions(self):
        self.assertRegex(TEXT, r"(?m)^permissions: \{\}$")

    def test_agent_job_token_is_read_only_and_has_no_oidc(self):
        self.assertEqual(["contents: read", "pull-requests: read"], permissions(job("review")))

    def test_agent_gets_the_jobs_own_token_not_an_app_token(self):
        self.assertIn("github_token: ${{ github.token }}", job("review"))

    def test_checkout_keeps_no_token(self):
        self.assertIn("persist-credentials: false", job("review"))


class AgentMayOnlyReadTheCheckout(unittest.TestCase):
    def test_allowed_tools_are_exactly_read_search_and_the_diff(self):
        self.assertEqual(["Read", "Grep", "Glob", "Bash(gh pr diff:*)"], tool_list("allowedTools"))

    def test_writing_the_web_and_subagents_are_denied(self):
        denied = tool_list("disallowedTools")
        for tool in ["Edit", "Write", "MultiEdit", "NotebookEdit", "WebFetch", "WebSearch", "Task"]:
            self.assertIn(tool, denied)

    def test_reading_where_keys_and_the_environment_live_is_denied(self):
        denied = tool_list("disallowedTools")
        for path in ["Read(./.git/**)", "Read(~/.*)", "Read(~/.*/**)", "Read(~/work/_temp/**)", "Read(//proc/**)",
                     "Read(//etc/**)", "Read(//tmp/**)", "Read(//root/**)"]:
            self.assertIn(path, denied)

    def test_the_review_comes_back_as_structured_output(self):
        self.assertIn("--json-schema", job("review"))
        self.assertIn("review: ${{ steps.agent.outputs.structured_output }}", job("review"))


class PostingJobChecksForSecrets(unittest.TestCase):
    def test_posting_job_runs_no_ai_and_only_writes_pull_requests(self):
        post = job("post")
        self.assertNotIn("claude-code-action", post)
        self.assertEqual(["pull-requests: write"], permissions(post))

    def test_untrusted_text_reaches_the_script_only_through_the_environment(self):
        run = job("post").split("run: |", 1)[1]
        self.assertNotIn("${{", run)

    def test_the_secret_check_comes_before_posting(self):
        post = job("post")
        self.assertLess(post.index('grep -qF -- "$API_KEY" review.md'), post.index("gh pr comment"))

    def test_the_pattern_catches_key_and_token_shapes(self):
        for secret in ["sk-ant-api03-AbCdEf0123456789xyz", "ghs_" + "a" * 36, "ghp_" + "B" * 36,
                       "github_pat_" + "c" * 40]:
            self.assertRegex(f"text {secret} text", secret_pattern())

    def test_the_pattern_lets_a_normal_review_through(self):
        review = "### Assertion review\n- `BolaTests.cs:52` rule 1: 200 only (sk-ant- is a prefix, ghs_ too)."
        self.assertNotRegex(review, secret_pattern())


class Trigger(unittest.TestCase):
    def test_no_review_on_every_push(self):
        self.assertIn("types: [opened, ready_for_review, reopened, labeled]", TEXT)
        self.assertNotIn("synchronize", TEXT)

    def test_only_the_ai_review_label_starts_a_run_and_drafts_never(self):
        condition = job("review")
        self.assertIn("github.event.label.name == 'ai-review'", condition)
        self.assertIn("github.event.pull_request.draft == false", condition)


if __name__ == "__main__":
    unittest.main()
