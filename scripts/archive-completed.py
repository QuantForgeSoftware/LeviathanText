#!/usr/bin/env python3
"""Cycle-end archive: move completed checklist items from todo.md to completed.md.

Standing rule (lessons.md #4): at the end of every work cycle, completed items leave
todo.md; open/in-progress items stay and plan narrative/reference records live in
project-plan.md. The archive is verbatim and grouped under the original section headings.

Usage: python3 scripts/archive-completed.py [--dry-run]
"""
import datetime
import re
import subprocess
import sys

TODO = "todo.md"
DONE = "completed.md"
NOTE = "*(All checklist items in this section are completed and archived in [completed.md](completed.md).)*"


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def main() -> int:
    dry = "--dry-run" in sys.argv
    todo = read(TODO)
    try:
        archive = read(DONE)
    except FileNotFoundError:
        archive = "# Completed work archive\n"

    lines = todo.split("\n")
    heading = re.compile(r"^(#{1,3}) (.*)$")
    bullet = re.compile(r"^- \[(x| |~)\] ")

    groups, order = {}, []
    kept = []
    removed = 0
    h2 = h3 = ""
    i = 0
    while i < len(lines):
        line = lines[i]
        m = heading.match(line)
        if m:
            level, title = m.group(1), m.group(2)
            if level == "##":
                h2, h3 = title, ""
            elif level == "###":
                h3 = title
            kept.append(line)
            i += 1
            continue

        b = bullet.match(line)
        if b:
            block = [line]
            j = i + 1
            while j < len(lines) and lines[j].startswith("  ") and lines[j].strip():
                block.append(lines[j])
                j += 1
            if b.group(1) == "x":
                key = (h2, h3)
                if key not in groups:
                    groups[key] = []
                    order.append(key)
                groups[key].extend(block)
                removed += 1
            else:
                kept.extend(block)
            i = j
            continue

        kept.append(line)
        i += 1

    if removed == 0:
        print("no completed items to archive")
        return 0

    # Append new groups to the archive (bdry: keep existing archive content).
    archive_lines = archive.rstrip().split("\n")
    archive_lines.append("")
    current_h2 = current_h3 = None
    for (g2, g3) in order:
        if g2 != current_h2:
            archive_lines.append(f"## {g2}" if g2 else "## (unheaded)")
            current_h2, current_h3 = g2, None
        if g3 and g3 != current_h3:
            archive_lines.append(f"### {g3}")
            current_h3 = g3
        if not g3:
            current_h3 = None
        archive_lines.extend(groups[(g2, g3)])

    sha = subprocess.check_output(["git", "rev-parse", "--short", "HEAD"]).decode().strip()
    today = datetime.date.today().isoformat()
    text = "\n".join(archive_lines).rstrip() + "\n"
    text = re.sub(r"Archive build: .*?\n", f"Archive build: {today} (archive run from `{sha}`).\n", text, count=1)
    count = len(re.findall(r"^- \[x\] ", text, flags=re.MULTILINE))
    text = re.sub(r"(Entries are verbatim, including commit references and links\. )\d+ items\.",
                  rf"\g<1>{count} items.", text, count=1)

    # Section notes where a section's items are all archived.
    out = []
    i = 0
    headings = re.compile(r"^(##|###) ")
    open_bullet = re.compile(r"^- \[( |~)\] ")
    while i < len(kept):
        line = kept[i]
        m = headings.match(line)
        if not m:
            out.append(line)
            i += 1
            continue
        level = len(m.group(1))
        j = i + 1
        has_open = False
        while j < len(kept):
            h = headings.match(kept[j])
            if h and len(h.group(1)) <= level:
                break
            if open_bullet.match(kept[j]):
                has_open = True
            j += 1
        out.append(line)
        title = line[len(m.group(1)) + 1:]
        moved_titles = {g[1] if level == 3 else g[0] for g in order}
        if not has_open and title in moved_titles:
            out.extend(["", NOTE])
        i += 1

    if dry:
        print(f"[dry-run] would archive {removed} items into {len(order)} groups")
        return 0

    with open(TODO, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out).rstrip() + "\n")
    with open(DONE, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(f"archived {removed} items into {len(order)} groups; wrote {DONE}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
