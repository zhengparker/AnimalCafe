"""Offline CI contract tests / 不启动 Unity、不读取账号或 Secrets。"""
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from unity_regression import filters, verify


class RegressionContractTests(unittest.TestCase):
    def reports(self, folder, *, missing=False, failed=False, count_delta=0):
        for mode, count in (("edit", 1459 + count_delta), ("play", 180)):
            if missing and mode == "play":
                continue
            root = ET.Element("test-run", result="Passed", passed=str(count),
                              failed="0", skipped="0", inconclusive="0", duration="1")
            selected = filters(mode)
            for index in range(count):
                selector = selected[index % len(selected)]
                ET.SubElement(root, "test-case", fullname=f"{selector}.Case{index}", result="Passed")
            if failed and mode == "play":
                root.set("failed", "1")
                root[0].set("result", "Failed")
            ET.ElementTree(root).write(Path(folder) / f"{mode}mode-results.xml")

    def test_exact_shared_scope_and_no_diagnostic_flags(self):
        self.assertEqual(len(filters("edit")), 4)
        self.assertEqual(len(filters("play")), 100)
        self.assertTrue(all(item.startswith("AnimalCafe.Tests.") for item in filters("all")))

    def test_complete_compact_reports_pass_and_report_skip(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            tree.getroot().set("passed", "179")
            tree.getroot().set("skipped", "1")
            tree.getroot()[-1].set("result", "Skipped")
            tree.write(path)
            result = verify(Path(folder), "compact")
            self.assertEqual(result[1]["skipped"], 1)

    def test_missing_suite_cannot_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder, missing=True)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_real_failure_cannot_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder, failed=True)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_all_skipped_is_not_a_successful_suite(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            tree.getroot().set("passed", "0")
            tree.getroot().set("skipped", "180")
            tree.getroot().set("result", "Skipped")
            for case in tree.getroot():
                case.set("result", "Skipped")
            tree.write(path)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_inconclusive_case_cannot_be_hidden_by_green_summary(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            tree.getroot()[0].set("result", "Inconclusive")
            tree.write(path)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_smaller_discovery_cannot_silently_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder, count_delta=-1)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_out_of_scope_case_cannot_replace_selected_case(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            tree.getroot()[0].set("fullname", "Wrong.Namespace.UnrelatedTest")
            tree.write(path)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_duplicate_cases_cannot_hide_missing_coverage(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            tree.getroot()[0].set("fullname", tree.getroot()[1].get("fullname"))
            tree.write(path)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")

    def test_every_selected_namespace_or_method_must_be_discovered(self):
        with tempfile.TemporaryDirectory() as folder:
            self.reports(folder)
            path = Path(folder) / "playmode-results.xml"
            tree = ET.parse(path)
            for index, case in enumerate(tree.getroot()):
                if case.get("fullname").startswith(filters("play")[-1] + "."):
                    case.set("fullname", filters("play")[0] + f".Replacement{index}")
            tree.write(path)
            with self.assertRaises(ValueError):
                verify(Path(folder), "compact")


if __name__ == "__main__":
    unittest.main()
