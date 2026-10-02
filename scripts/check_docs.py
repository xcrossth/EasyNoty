#!/usr/bin/env python3
"""Check bilingual Markdown pairs and repository-local links (no dependencies)."""

from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=ROOT, check=True, capture_output=True,
    )
    names = set(result.stdout.decode("utf-8").split("\0")) - {""}
    pages = sorted(name for name in names if name.endswith(".md"))
    errors: list[str] = []
    for name in pages:
        page = ROOT / name
        if not page.is_file():
            errors.append(f"{name}: missing file")
            continue
        text = page.read_text(encoding="utf-8-sig")
        partner = (name[:-6] + ".md") if name.endswith(".en.md") else (name[:-3] + ".en.md")
        if partner not in names:
            errors.append(f"{name}: missing language partner {partner}")
        partner_link = f"]({Path(partner).name})"
        if partner_link not in text:
            errors.append(f"{name}: missing reciprocal language link")
        # Ignore fenced examples: only rendered Markdown links are checked.
        rendered = re.sub(r"```[^\n]*\n.*?```", "", text, flags=re.S)
        for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", rendered):
            target = target.strip().strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            resolved = (page.parent / unquote(parsed.path)).resolve()
            if not resolved.is_relative_to(ROOT):
                errors.append(f"{name}: link escapes repository: {target}")
            elif not resolved.exists():
                errors.append(f"{name}: broken local link: {target}")
        if not name.endswith(".en.md") and not re.search(r"[\u0e00-\u0e7f]", text):
            errors.append(f"{name}: default page must contain Thai text")
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"PASS: {len(pages)} Markdown pages, {len(pages) // 2} language pairs, local links resolved")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
