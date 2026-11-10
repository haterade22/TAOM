from pathlib import Path


def report_path_for_output(output):
    output = Path(output)
    return output.with_name(f"{output.stem}-retopo-report.json")
