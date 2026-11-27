"""Tests for scripts/check_assertion_rules.py (gate G4 g). Run: python3 -m unittest discover -s scripts/tests"""
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import check_assertion_rules  # noqa: E402

CS = "backend/tests/HospitalManagement.UnitTests/Sample/SampleTests.cs"
TS = "frontend-web/src/pages/Sample.test.tsx"


def cs_test(body):
    """A C# test method with the given body lines."""
    lines = ["public class SampleTests", "{", "    [Fact]", "    public async Task Sample()", "    {"]
    lines += ["        " + line for line in body.strip("\n").split("\n")]
    lines += ["    }", "}"]
    return "\n".join(lines)


def findings(text, path=CS):
    return check_assertion_rules.check({path: text})


def rules(text, path=CS):
    return [f.split(" ")[2] + " " + f.split(" ")[3].rstrip(":") for f in findings(text, path)]


class Rule15ConditionalAsserting(unittest.TestCase):
    def test_assertion_in_if_block_is_flagged(self):
        self.assertEqual(["rule 15"], rules(cs_test("if (x)\n{\n    Assert.Equal(1, y);\n}")))

    def test_assertion_in_same_line_if_is_flagged(self):
        self.assertEqual(["rule 15"], rules(cs_test("if (x) Assert.Equal(1, y);")))

    def test_assertion_in_single_statement_if_on_next_line_is_flagged(self):
        self.assertEqual(["rule 15"], rules(cs_test("if (x)\n    Assert.Equal(1, y);")))

    def test_assertion_in_else_and_switch_case_is_flagged(self):
        text = cs_test('if (x) { y = 1; } else { Assert.Equal(1, y); }\n'
                       'switch (e)\n{\n    case "401": Assert.Equal(401, s); break;\n}')
        self.assertEqual(["rule 15", "rule 15"], rules(text))

    def test_assertion_in_catch_is_flagged_but_assert_fail_is_allowed(self):
        self.assertEqual(["rule 15"], rules(cs_test("try { Act(); }\ncatch (Exception)\n{\n    Assert.Equal(1, y);\n}")))
        self.assertEqual([], rules(cs_test("try { Act(); }\ncatch (Exception ex)\n{\n    Assert.Fail(ex.Message);\n}")))

    def test_assertion_after_the_branch_is_not_flagged(self):
        self.assertEqual([], rules(cs_test("if (x)\n{\n    y = 1;\n}\nAssert.Equal(1, y);")))

    def test_if_inside_a_string_is_ignored(self):
        self.assertEqual([], rules(cs_test('var s = "if (x) {";\nAssert.Equal("a", s, "the text");')))


class Rule19Messages(unittest.TestCase):
    def test_assert_true_without_message_is_flagged(self):
        self.assertEqual(["rule 19"], rules(cs_test("Assert.True(result.Success);")))

    def test_assert_true_with_message_passes_even_across_lines_and_with_commas_inside(self):
        self.assertEqual([], rules(cs_test('Assert.True(F(a, b),\n    "must hold");')))

    def test_comma_inside_a_string_is_not_a_message(self):
        self.assertEqual(["rule 19"], rules(cs_test('Assert.False(s.Contains("a,b"));')))


class Rule12FaultedTasks(unittest.TestCase):
    def test_is_completed_alone_is_flagged(self):
        self.assertIn("rule 12", rules(cs_test('Assert.True(task.IsCompleted, "done");')))

    def test_is_completed_with_is_faulted_passes(self):
        text = cs_test('Assert.True(task.IsCompleted, "done");\nAssert.False(task.IsFaulted, "no error");')
        self.assertEqual([], rules(text))


class Rule7EmptyCollections(unittest.TestCase):
    def test_assert_all_without_proof_of_content_is_flagged(self):
        self.assertEqual(["rule 7"], rules(cs_test("Assert.All(items.EnumerateArray(), i => Assert.Equal(1, i));")))

    def test_assert_all_after_not_empty_or_contains_on_the_same_collection_passes(self):
        self.assertEqual([], rules(cs_test(
            "Assert.NotEmpty(items.EnumerateArray());\nAssert.All(items.EnumerateArray(), i => Assert.Equal(1, i));")))
        self.assertEqual([], rules(cs_test(
            "Assert.Contains(list, a => a == 1);\nAssert.All(list, a => Assert.Equal(1, a));")))

    def test_proof_on_another_collection_does_not_count(self):
        self.assertEqual(["rule 7"], rules(cs_test(
            "Assert.NotEmpty(others);\nAssert.All(items, i => Assert.Equal(1, i));")))


class Rule16Specificity(unittest.TestCase):
    def test_count_with_greater_or_equal_is_flagged(self):
        self.assertEqual(["rule 16"], rules(cs_test('Assert.True(list.Count >= 1, "some");')))

    def test_comparison_that_is_not_a_count_passes(self):
        self.assertEqual([], rules(cs_test('Assert.True(next > now, "in the future");')))

    def test_component_test_bounds_and_partial_bodies_are_flagged(self):
        text = "expect(items.length).toBeGreaterThan(0);\nexpect(onSubmit.mock.calls[0][0]).toMatchObject({ a: 1 });"
        self.assertEqual(["rule 16", "rule 16"], rules(text, TS))

    def test_to_match_object_on_something_else_passes(self):
        self.assertEqual([], rules("expect(state).toMatchObject({ a: 1 });", TS))


class ExceptionComments(unittest.TestCase):
    def test_a_reasoned_exception_accepts_the_finding_below_it(self):
        text = cs_test("// Assertion rule 19 exception: the bool is the whole contract here\nAssert.True(ok);")
        self.assertEqual([], rules(text))

    def test_an_exception_for_another_rule_does_not_accept_it(self):
        text = cs_test("// Assertion rule 7 exception: the bool is the whole contract here\nAssert.True(ok);")
        self.assertEqual(["rule 19"], rules(text))

    def test_an_exception_without_reason_or_with_an_unknown_rule_is_a_finding(self):
        self.assertEqual(["exception comment"], rules(cs_test("// Assertion rule 3 exception:\nx = 1;")))
        self.assertEqual(["exception comment"], rules(cs_test("// Assertion rule 21 exception: no such rule here\nx = 1;")))

    def test_the_exception_reaches_ten_lines_and_no_further(self):
        filler = "\n".join(["x = 1;"] * 11)
        text = cs_test("// Assertion rule 19 exception: explained once for this block\n" + filler + "\nAssert.True(ok);")
        self.assertEqual(["rule 19"], rules(text))


class Report(unittest.TestCase):
    def test_a_finding_names_the_file_line_and_rule(self):
        self.assertEqual([f"G4(g) {CS}:6 rule 19: Assert.True without a message: say what was expected, "
                          "or use Equal / Single / Contains"], findings(cs_test("Assert.True(ok);")))

    def test_the_repository_files_are_found(self):
        files = list(check_assertion_rules.source_files())
        names = {p.name for p in files}
        self.assertIn("BolaTests.cs", names)
        self.assertIn("PatientForm.test.tsx", names)
        self.assertFalse(any("bin" in p.parts or "node_modules" in p.parts for p in files))


if __name__ == "__main__":
    unittest.main()
