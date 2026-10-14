"""Offline regression tests: no GitHub calls or Unity login.
离线验证认证保护，不连接 GitHub 或 Unity。
"""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "guard", Path(__file__).with_name("unity-authentication-guard.py")
)
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class AuthenticationGuardTests(unittest.TestCase):
    def history(self, markers, conclusions=None):
        conclusions = conclusions or ["failure"] * len(markers)
        runs = [{"id": 20-i, "conclusion": c, "run_attempt": 1,
                 "run_started_at": f"2026-10-13T12:{50-i:02d}:00Z"} for i, c in enumerate(conclusions)]

        def get_json(path):
            if "workflow" in path:
                return {"workflow_runs": runs if "page=1&" in path else []}
            run_id = int(path.split("/")[2])
            if "/jobs?" in path:
                return {"jobs": [{"steps": [{"name": "Run complete Unity test suites after one activation",
                    "conclusion": "success"}]}]} if conclusions[20-run_id] == "success" else {"jobs": []}
            return {"artifacts": [{"name": name + "-attempt-1"} for name in markers[20-run_id]]}

        return get_json

    def test_failed_activation_stops_new_automatic_login(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.history([[guard.HALTED]]), 99)

    def test_skipped_preflight_does_not_clear_old_halt(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.history([[], [], [guard.HALTED]]), 99)

    def test_normal_test_failure_can_be_retested_after_fix(self):
        guard.check_previous_runs(self.history([[guard.OK]]), 99)

    def test_confirmed_later_success_clears_old_halt(self):
        guard.check_previous_runs(self.history([[guard.OK], [guard.HALTED]]), 99)

    def test_focused_diagnosis_cannot_clear_old_halt(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.history([["unity-authentication-diagnostic"], [guard.HALTED]]), 99)

    def test_old_success_allows_migration_to_new_guard(self):
        guard.check_previous_runs(self.history([[]], ["success"]), 99)

    def test_current_run_cannot_authorize_itself(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.history([[guard.OK], [guard.HALTED]]), 20)

    def test_conflicting_markers_fail_closed(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.history([[guard.OK, guard.HALTED]]), 99)

    def test_api_error_stops_before_login(self):
        def unavailable(path):
            raise OSError("API unavailable")
        with self.assertRaises(OSError):
            guard.check_previous_runs(unavailable, 99)

    def test_missing_checkpoint_after_real_attempt_fails_closed(self):
        def no_upload(path):
            if "workflow" in path:
                return {"workflow_runs": [{"id": 10, "conclusion": "failure", "run_attempt": 1,
                    "run_started_at": "2026-10-13T12:00:00Z"}]}
            if "/jobs?" in path:
                return {"jobs": [{"steps": [{"name": "Run complete Unity test suites after one activation", "conclusion": "failure"}]}]}
            return {"artifacts": []}
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(no_upload, 99)

    def test_history_pagination_finds_older_halt(self):
        def paged(path):
            if "workflow" in path:
                if "page=1&" in path:
                    return {"workflow_runs": [{"id": i, "conclusion": "failure", "run_attempt": 1,
                        "run_started_at": "2026-10-13T12:00:00Z"} for i in range(200, 100, -1)]}
                return {"workflow_runs": [{"id": 100, "conclusion": "failure", "run_attempt": 1,
                    "run_started_at": "2026-10-13T12:00:00Z"}]}
            if "/jobs?" in path:
                return {"jobs": []}
            return {"artifacts": [{"name": guard.HALTED + "-attempt-1"}] if "/100/" in path else []}
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(paged, 999)

    def rerun_history(self, lost_checkpoint=False, current=False):
        runs = [
            {"id": 20, "conclusion": "success", "run_attempt": 1, "run_started_at": "2026-10-13T12:00:00Z"},
            {"id": 10, "conclusion": "cancelled" if lost_checkpoint else "failure",
             "run_attempt": 2, "run_started_at": "2026-10-13T13:00:00Z"},
        ]
        def get_json(path):
            if "workflow" in path:
                return {"total_count": 2, "workflow_runs": runs}
            run_id = int(path.split("/")[2])
            if path.endswith("/attempts/1"):
                return {"id": 10, "run_attempt": 1, "conclusion": "success", "run_started_at": "2026-10-13T11:00:00Z"}
            if "/jobs?" in path:
                return {"jobs": [{"steps": [{"name": "Run complete Unity test suites after one activation", "conclusion": "cancelled"}]}]}
            names = [guard.OK + "-attempt-1"]
            if run_id == 10 and not lost_checkpoint:
                names.append(guard.HALTED + "-attempt-2")
            return {"artifacts": [{"name": name} for name in names]}
        return get_json

    def test_later_rerun_failure_wins_over_newer_created_success(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.rerun_history(), 99)

    def test_old_checkpoint_cannot_mask_later_cancelled_attempt(self):
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(self.rerun_history(lost_checkpoint=True), 99)

    def test_incomplete_history_fails_closed(self):
        def truncated(path):
            return {"total_count": 1001, "workflow_runs": []}
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(truncated, 99)

    def test_current_rerun_checks_its_own_prior_failed_attempt(self):
        def prior(path):
            if "workflow" in path:
                return {"total_count": 1, "workflow_runs": [{"id": 10, "run_attempt": 3,
                    "status": "in_progress", "conclusion": None, "run_started_at": "2026-10-13T14:00:00Z"}]}
            if path.endswith("/attempts/2"):
                return {"status": "completed", "conclusion": "failure", "run_started_at": "2026-10-13T13:00:00Z"}
            if path.endswith("/attempts/1"):
                return {"status": "completed", "conclusion": "success", "run_started_at": "2026-10-13T12:00:00Z"}
            return {"artifacts": [{"name": guard.HALTED + "-attempt-2"}, {"name": guard.OK + "-attempt-1"}]}
        with self.assertRaises(guard.AuthenticationHalted):
            guard.check_previous_runs(prior, 10)

    def record(self, reports, scope="full"):
        temp_root = Path(__file__).resolve().parents[2] / "outputs/ci-guard-tests"
        temp_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=temp_root) as directory:
            base = Path(directory)
            for name, text in reports.items():
                (base / name).write_text(text)
            marker = guard.record_attempt(base, base / "state", scope)
            data = json.loads((base / "state" / "state.json").read_text())
            self.assertEqual(marker, data["marker"])
            return marker

    def test_missing_reports_halt_future_automatic_runs(self):
        self.assertEqual(guard.HALTED, self.record({}))

    def test_incomplete_or_invalid_reports_halt(self):
        for reports in [
            {"editmode-results.xml": '<test-run total="1" />'},
            {"editmode-results.xml": '<test-run total="1" />', "playmode-results.xml": "broken"},
            {"editmode-results.xml": '<test-run total="0" />', "playmode-results.xml": '<test-run total="1" />'},
        ]:
            with self.subTest(reports=reports):
                self.assertEqual(guard.HALTED, self.record(reports))

    def test_real_test_failures_are_not_authentication_failures(self):
        reports = {f"{suite}-results.xml": '<test-run total="2" failed="1" />' for suite in ["editmode", "playmode"]}
        self.assertEqual(guard.OK, self.record(reports))

    def test_focused_reports_do_not_authorize_full_automatic_login(self):
        reports = {f"{suite}-results.xml": '<test-run total="1" />' for suite in ["editmode", "playmode"]}
        self.assertEqual("unity-authentication-diagnostic", self.record(reports, "remaining-ci-failures"))

    def test_failed_focused_activation_still_sets_halt(self):
        self.assertEqual(guard.HALTED, self.record({}, "remaining-ci-failures"))


if __name__ == "__main__":
    unittest.main()
