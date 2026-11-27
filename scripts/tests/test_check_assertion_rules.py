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

    def test_an_exception_covers_only_the_next_assertion(self):
        text = cs_test("// Assertion rule 19 exception: the bool is the whole contract here\nAssert.True(a);\nAssert.True(b);")
        self.assertEqual(["rule 19"], rules(text))

    def test_an_exception_inside_a_string_is_not_an_exception(self):
        text = cs_test('var s = "// Assertion rule 19 exception: not a comment at all";\nAssert.True(ok);')
        self.assertEqual(["rule 19"], rules(text))


class ReviewFindingsOfPR74(unittest.TestCase):
    """Cases qe-reviewer found in the first version of the script: misses (C3-C7) and false alarms (C8)."""

    # ---------- misses ----------
    def test_multi_line_if_condition(self):
        self.assertEqual(["rule 15"], rules(cs_test("if (a == 1\n    && b > 0)\n{\n    Assert.Equal(1, x);\n}")))

    def test_early_return_before_an_assertion(self):
        self.assertEqual(["rule 15"], rules(cs_test("if (s == 404) return;\nAssert.Equal(1, x);")))

    def test_foreach_over_a_collection_not_proven_non_empty(self):
        self.assertEqual(["rule 7"], rules(cs_test("foreach (var a in list) Assert.Equal(1, a);")))

    def test_foreach_after_proof_passes(self):
        self.assertEqual([], rules(cs_test("Assert.NotEmpty(list);\nforeach (var a in list)\n{\n    Assert.Equal(1, a);\n}")))

    def test_lower_bound_on_a_name_without_count(self):
        self.assertEqual(["rule 16"], rules(cs_test('Assert.True(recentlyFailed >= 1, "x");')))

    def test_equal_to_zero_is_no_proof_of_content(self):
        self.assertEqual(["rule 7"], rules(cs_test(
            "Assert.Equal(0, items.GetArrayLength());\nAssert.All(items.EnumerateArray(), i => Assert.Equal(1, i));")))

    def test_an_unrelated_equal_that_mentions_the_name_is_no_proof(self):
        self.assertEqual(["rule 7"], rules(cs_test(
            "Assert.Equal(HttpStatusCode.OK, response.StatusCode);\nAssert.All(response.Items, i => Assert.Equal(1, i));")))

    def test_empty_or_null_message(self):
        self.assertEqual(["rule 19", "rule 19"], rules(cs_test('Assert.True(ok, "");\nAssert.False(bad, null);')))

    def test_is_faulted_in_a_comment_does_not_count(self):
        self.assertIn("rule 12", rules(cs_test('Assert.True(stop.IsCompleted, "ended"); // IsFaulted not needed')))

    def test_partial_body_through_object_containing(self):
        self.assertEqual(["rule 16"], rules("expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ a: 1 }));", TS))

    def test_to_match_object_on_a_body_read_further_up(self):
        text = ("const sent = onSubmit.mock.calls[0][0];\nconst x = 1;\nconst y = 2;\nconst z = 3;\n"
                "expect(sent).toMatchObject({ a: 1 });")
        self.assertEqual(["rule 16"], rules(text, TS))

    # ---------- false alarms ----------
    def test_generic_type_argument_is_not_a_comparison(self):
        self.assertEqual([], rules(cs_test('Assert.True(items.OfType<Doctor>().Count() == 2, "two doctors");')))

    def test_an_upper_bound_is_a_real_requirement(self):
        self.assertEqual([], rules(cs_test('Assert.True(token.Length <= 64, "at most 64 characters");')))

    def test_an_assertion_inside_a_comment_is_ignored(self):
        self.assertEqual([], rules(cs_test("// before: Assert.True(ok);\nx = 1;")))

    def test_try_finally_is_not_a_branch(self):
        self.assertEqual([], rules(cs_test("try\n{\n    Assert.Equal(1, x);\n}\nfinally\n{\n    Cleanup();\n}")))

    def test_try_with_a_catch_around_an_assertion_is_flagged(self):
        self.assertEqual(["rule 15"], rules(cs_test("try\n{\n    Assert.Equal(1, x);\n}\ncatch (Exception)\n{\n    y = 2;\n}")))

    def test_assert_all_over_a_literal_array(self):
        self.assertEqual([], rules(cs_test("Assert.All(new[] { a, b }, i => Assert.Equal(1, i));")))

    def test_verbatim_string_does_not_unbalance_the_braces(self):
        text = cs_test('var p = @"C:\\dir\\";\nif (x) { y = 1; }\nAssert.Equal(1, y);')
        self.assertEqual([], rules(text))


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
