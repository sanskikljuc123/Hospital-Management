"""Tests for scripts/check_ids.py (gate G4 e). Run: python3 -m unittest discover -s scripts/tests"""
import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import check_ids  # noqa: E402

T = pathlib.Path("docs/testing")
REGISTER = T / "RISK_REGISTER.md"
UI = T / "UI_TEST_DESIGN.md"
FLOWS = T / "BUSINESS_FLOWS.md"
GATES = T / "QUALITY_GATES.md"
ARCH = pathlib.Path("ARCHITECTURE.md")


def documents(**changes):
    """A minimal, consistent set of owning documents; keyword arguments replace one document's text."""
    docs = {
        REGISTER: "## 2. Risks\n| R-001 | risk |\n## 3. Assumptions\n| A-001 | a |\n"
                  "## 4. Open questions\n| Q-001 | q |\n## 5. Accepted risks\n| R-001 | accepted |\n",
        UI: "## 3. Defects found while designing\n| D-2 | Fact |\n## 6. Test catalogue\n| UI-HP-05 | t |\n"
            "## 7. Gaps and missing requirements\n| Q-a | Answered |\n",
        FLOWS: "## 3. Flows\n### F-02 Patient registration\n| F-02.6 | Doctor tries | 403 |\n",
        GATES: "## 1. Gates\n| G9 | Mutation |\n",
        ARCH: "## 11. Decision log\n| D-9 | Where authorization lives |\nSee D-9.\n",
    }
    names = {"register": REGISTER, "ui": UI, "flows": FLOWS, "gates": GATES, "arch": ARCH}
    for name, text in changes.items():
        docs[names[name]] = text
    return docs


def run(docs, extra=None):
    files = dict(docs)
    files.update(extra or {})
    return check_ids.check(docs, files)


class DefinitionsTest(unittest.TestCase):
    def test_consistent_documents_pass(self):
        self.assertEqual(run(documents()), [])

    def test_an_id_defined_twice_in_its_section_fails(self):
        docs = documents(gates="## 1. Gates\n| G9 | Mutation |\n| G9 | Again |\n")
        failures = run(docs)
        self.assertEqual(len(failures), 1)
        self.assertIn("gate G9 is defined 2 times", failures[0])

    def test_a_row_in_another_section_is_neither_a_definition_nor_a_duplicate(self):
        # R-001 has a row only under "Accepted risks", so it is not defined, and that row is itself a reference.
        docs = documents(register="## 2. Risks\n| R-002 | risk |\n## 3. Assumptions\n| A-001 | a |\n"
                                  "## 4. Open questions\n| Q-001 | q |\n## 5. Accepted risks\n| R-001 | accepted |\n")
        failures = run(docs, {T / "README.md": "R-001"})
        self.assertEqual(failures, [
            "docs/testing/RISK_REGISTER.md:8 names risk R-001, which is not defined in "
            "docs/testing/RISK_REGISTER.md '2. Risks'",
            "docs/testing/README.md:1 names risk R-001, which is not defined in "
            "docs/testing/RISK_REGISTER.md '2. Risks'",
        ])

    def test_only_the_row_in_the_owning_section_is_a_definition(self):
        # The default register has R-001 in §2 (line 2) and in §5 (line 8); only §2 defines it.
        definitions, _ = check_ids.find_definitions(documents())
        self.assertEqual(definitions[("risk", "R-001")], [(REGISTER, 2)])

    def test_a_missing_owning_section_fails_instead_of_emptying_the_kind(self):
        failures = run(documents(gates="## 1. Gate list\n| G9 | Mutation |\n"))
        self.assertEqual(failures[0], "docs/testing/QUALITY_GATES.md has no section '## 1. Gates'; "
                                      "it owns every gate ID")


class ReferencesTest(unittest.TestCase):
    def test_every_kind_of_undefined_reference_fails(self):
        line = "R-002 A-002 Q-002 Q-b D-3 UI-HP-06 F-03 F-02.7 G11"
        failures = run(documents(), {T / "README.md": line})
        kinds = [f.split(" names ")[1].split(",")[0] for f in failures]
        self.assertEqual(kinds, ["risk R-002", "assumption A-002", "question Q-002", "ui question Q-b",
                                 "defect D-3", "ui test UI-HP-06", "flow F-03", "scenario F-02.7", "gate G11"])

    def test_defined_references_pass(self):
        line = "R-001 A-001 Q-001 Q-a D-2 UI-HP-05 F-02 F-02.6 G9"
        self.assertEqual(run(documents(), {T / "README.md": line}), [])

    def test_a_scenario_is_not_read_as_its_flow(self):
        docs = documents(flows="## 3. Flows\n### F-02 Patient registration\n")
        failures = run(docs, {pathlib.Path("e2e/Features/Patients.feature"): "@F-02.6\nScenario: x"})
        self.assertEqual(failures, ["e2e/Features/Patients.feature:1 names scenario F-02.6, which is not defined "
                                    "in docs/testing/BUSINESS_FLOWS.md '3. Flows'"])

    def test_a_scenario_trait_in_a_test_must_exist(self):
        test = pathlib.Path("backend/tests/HospitalManagement.ApiTests/Security/PatientWriteAccessTests.cs")
        failures = run(documents(), {test: '    [Trait("Scenario", "F-02.9")]'})
        self.assertEqual(len(failures), 1)
        self.assertIn(f"{test.as_posix()}:1 names scenario F-02.9", failures[0])

    def test_an_id_inside_a_longer_token_is_not_a_reference(self):
        # R-002 is not defined, so each token fails if the look-behind or look-ahead stops excluding it.
        tokens = "R-0069 XR-002 R-002x R-002.1 R-002-x -R-002 G9a G11a F-02.6.1 F-03.1.2 Q-ab"
        self.assertEqual(run(documents(), {T / "README.md": tokens}), [])

    def test_a_d_number_in_architecture_is_a_decision(self):
        self.assertEqual(run(documents(), {ARCH: documents()[ARCH] + "D-9"}), [])
        failures = run(documents(), {ARCH: documents()[ARCH] + "D-2"})
        self.assertEqual(failures, ["ARCHITECTURE.md:4 names decision D-2, which is not defined in "
                                    "ARCHITECTURE.md '11. Decision log'"])

    def test_a_bare_d_number_elsewhere_is_a_defect(self):
        failures = run(documents(), {T / "README.md": "D-9"})
        self.assertEqual(failures, ["docs/testing/README.md:1 names defect D-9, which is not defined in "
                                    "docs/testing/UI_TEST_DESIGN.md '3. Defects found while designing'"])

    def test_architecture_right_before_a_d_number_makes_it_a_decision(self):
        self.assertEqual(run(documents(), {T / "README.md": "ARCHITECTURE D-9"}), [])
        failures = run(documents(), {T / "README.md": "ARCHITECTURE D-2"})
        self.assertEqual(failures, ["docs/testing/README.md:1 names decision D-2, which is not defined in "
                                    "ARCHITECTURE.md '11. Decision log'"])

    def test_both_d_series_on_one_line_are_told_apart(self):
        # D-2 is only a defect and D-9 only a decision in the fixture, so a swap of either fails.
        line = '("ARCHITECTURE D-9", "UI_TEST_DESIGN D-2")'
        self.assertEqual(run(documents(), {T / "README.md": line}), [])

    def test_architecture_elsewhere_on_the_line_does_not_make_a_decision(self):
        failures = run(documents(), {T / "README.md": "(ARCHITECTURE.md §3, D-9)"})
        self.assertEqual(len(failures), 1)
        self.assertIn("names defect D-9", failures[0])

    def test_a_word_ending_in_architecture_does_not_make_a_decision(self):
        failures = run(documents(), {T / "README.md": "PRE_ARCHITECTURE D-9"})
        self.assertEqual(len(failures), 1)
        self.assertIn("names defect D-9", failures[0])


class FilesReadTest(unittest.TestCase):
    def test_which_files_are_read(self):
        read = {
            "docs/testing/README.md": True,
            "docs/images/make_testing_flow.py": False,
            ".claude/agents/qe-reviewer.md": True,
            ".claude/skills/evidence-rules/SKILL.md": True,
            "frontend-web/src/pages/Patients.test.tsx": True,
            "frontend-web/src/api/client.test.ts": True,
            "frontend-web/src/test/contract.ts": True,  # a test helper
            "frontend-web/src/test/setup.ts": True,
            "frontend-web/src/pages/Patients.tsx": False,  # application code, not a test
            "e2e/HospitalManagement.E2E/Features/Patients.feature": True,
            "e2e/HospitalManagement.E2E/Features/Patients.feature.cs": False,  # generated from the .feature
            "backend/tests/HospitalManagement.ApiTests/Security/PatientWriteAccessTests.cs": True,
            "backend/tests/HospitalManagement.ApiTests/bin/Release/Generated.cs": False,
            "backend/tests/HospitalManagement.ApiTests/appsettings.json": False,  # not a test source
            "frontend-web/src/node_modules/x/y.test.ts": False,
        }
        for path, expected in read.items():
            with self.subTest(path=path):
                self.assertEqual(check_ids.is_read(pathlib.Path(path)), expected)

    def test_read_files_reads_every_listed_file_and_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            for path in check_ids.FILES:
                write(root / path)
            samples = ["docs/testing/README.md", ".claude/agents/qe-reviewer.md",
                       ".claude/skills/evidence-rules/SKILL.md", "backend/tests/A/ATests.cs",
                       "e2e/Features/F.feature", "frontend-web/src/test/setup.ts", "frontend-web/src/pages/P.tsx"]
            for path in samples:
                write(root / path)
            files, missing = check_ids.read_files(root)
            self.assertEqual(missing, [])
            expected = {pathlib.Path(p) for p in samples[:-1]} | set(check_ids.FILES)
            self.assertEqual(set(files), expected)
            self.assertEqual(files[pathlib.Path("CLAUDE.md")], "G9")

    def test_a_missing_listed_file_or_directory_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            for path in check_ids.FILES:
                if path.name != "CLAUDE.md":
                    write(root / path)
            for path in check_ids.DIRECTORIES:
                if path.as_posix() != "e2e":
                    (root / path).mkdir(parents=True)
            _, missing = check_ids.read_files(root)
            self.assertEqual(missing, ["CLAUDE.md is missing; G4 (e) reads it", "e2e/ is missing; G4 (e) reads it"])
            _, failures = check_ids.run(root)
            self.assertEqual(failures[:2], missing)  # the gate fails on them, not only lists them


def write(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("G9", encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
