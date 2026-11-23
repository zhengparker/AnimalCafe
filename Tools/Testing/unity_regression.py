"""Shared local/CI Unity scope and NUnit verification / 共用范围与真实结果核查。"""
import argparse
from collections import Counter
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
EXPECTED = {"editmode-results.xml": 1522, "playmode-results.xml": 180}


def filters(mode):
    """Return the same selectors for local CLI and GameCI / 不加诊断参数。"""
    if mode == "all":
        return filters("edit") + filters("play")
    items = (HERE / f"compact-{mode}.filter.txt").read_text(encoding="utf-8-sig").strip().split(";")
    if len(items) != len(set(items)) or any(not re.fullmatch(r"AnimalCafe\.Tests\.[A-Za-z0-9_.]+", item) for item in items):
        raise ValueError(f"Empty, duplicate or invalid {mode} compact selector")
    return items


def verify(folder, scope):
    """Missing/failed/inconclusive/empty/discovery drift must fail / SKIP保留原状。"""
    reports = []
    for path in sorted(folder.rglob("*.xml")):
        root = ET.parse(path).getroot()
        if root.tag == "test-run":
            reports.append((path, root))
    if len(reports) != 2 or {path.name for path, _ in reports} != set(EXPECTED):
        raise ValueError("Both unique EditMode and PlayMode NUnit XML reports are required")
    results = []
    for path, root in reports:
        cases = list(root.iter("test-case"))
        counts = Counter(case.get("result") for case in cases)
        if not counts["Passed"] or set(counts) - {"Passed", "Skipped"} or int(root.get("failed", "0")) or int(root.get("inconclusive", "0")):
            raise ValueError(f"Incomplete or failed Unity test run: {path.name}: {dict(counts)}")
        if root.get("result") not in ("Passed", "Skipped", "Skipped:Ignored"):
            raise ValueError(f"Unsuccessful Unity report: {path.name}")
        if counts["Passed"] != int(root.get("passed", "0")) or counts["Skipped"] != int(root.get("skipped", "0")):
            raise ValueError(f"NUnit case/summary count mismatch: {path.name}")
        if scope == "compact":
            if len(cases) != EXPECTED[path.name]:
                raise ValueError(f"Compact discovery changed: {path.name}: {len(cases)} != {EXPECTED[path.name]}")
            selected = filters("edit" if path.name.startswith("edit") else "play")
            seen, covered = set(), set()
            for case in cases:
                name = case.get("fullname", "")
                if name in seen:
                    raise ValueError(f"Duplicate compact case: {name}")
                seen.add(name)
                matching = {item for item in selected if name == item or name.startswith(item + ".") or name.startswith(item + "(")}
                if not matching:
                    raise ValueError(f"Out-of-scope compact case: {name}")
                covered.update(matching)
            if covered != set(selected):
                raise ValueError(f"Missing compact selectors: {sorted(set(selected) - covered)}")
        results.append({"report": path.name, "passed": counts["Passed"], "skipped": counts["Skipped"],
                        "duration_minutes": float(root.get("duration", "0")) / 60,
                        "skips": [(case.get("fullname"), case.findtext("reason/message", "No reason recorded"))
                                  for case in cases if case.get("result") == "Skipped"]})
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    command = commands.add_parser("filter")
    command.add_argument("--mode", choices=("edit", "play", "all"), required=True)
    command = commands.add_parser("verify")
    command.add_argument("--scope", choices=("compact", "full", "remaining-ci-failures", "p15-ci-repairs", "p15-ci-five-failures"), required=True)
    command.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "filter":
        print(";".join(filters(args.mode)))
        return
    results = verify(args.results, args.scope)
    summary = f"Tested commit: `{os.environ.get('TESTED_COMMIT', 'local reports')}`\n\nScope: **{args.scope}**\n\n"
    for row in results:
        summary += f"{row['report']}: **{row['passed']} PASS / 0 FAIL / {row['skipped']} SKIP** ({row['duration_minutes']:.2f} min)\n\n"
        for name, reason in row["skips"]:
            summary += f"- SKIP, not PASS: `{name}` — {reason}\n"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as stream:
            stream.write(summary)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, ET.ParseError) as error:
        raise SystemExit(str(error))
