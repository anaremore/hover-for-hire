"""Zip release players under their version, preserving executable modes for macOS/Linux recipients.

Each platform folder must hold a release build of the current commit, as recorded in the build-info.json that
the Unity build writes beside the player. Pass --allow-stale to package a build of another commit, or one made
with uncommitted changes, anyway.
"""
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import hashlib
import json
import subprocess
import sys

project = Path(__file__).resolve().parent.parent
root = project / "Builds"
allow_stale = "--allow-stale" in sys.argv
head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=project, capture_output=True, text=True).stdout.strip()
problems = 0
for platform in ("Windows", "macOS", "Linux"):
    source = root / platform
    if not source.is_dir():
        continue
    info_path = source / "build-info.json"
    if not info_path.is_file():
        print(f"{platform}: no build-info.json; rebuild with Tools/Unity.ps1 {platform}")
        problems += 1
        continue
    info = json.loads(info_path.read_text(encoding="utf-8"))
    if info.get("Development"):
        print(f"{platform}: development build; archives need a release build (Tools/Unity.ps1 {platform})")
        problems += 1
        continue
    commit = info.get("Commit", "")
    if (commit != head or info.get("UncommittedChanges")) and not allow_stale:
        dirty = " with uncommitted changes" if info.get("UncommittedChanges") else ""
        print(f"{platform}: built from {commit[:8] or 'an unknown commit'}{dirty}, but HEAD is {head[:8]}; rebuild or pass --allow-stale")
        problems += 1
        continue
    version = info["Version"]
    output = root / f"Hover-for-Hire-{version}-{platform}.zip"
    with ZipFile(output, "w", ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(source.rglob("*")):
            if not path.is_file() or any("DoNotShip" in part for part in path.parts):
                continue
            relative = path.relative_to(source).as_posix()
            info_entry = ZipInfo(relative)
            info_entry.create_system = 3
            executable = ("/Contents/MacOS/" in f"/{relative}" or path.suffix in (".x86_64", ".so", ".dylib", ".exe"))
            info_entry.external_attr = (0o100755 if executable else 0o100644) << 16
            info_entry.compress_type = ZIP_DEFLATED
            archive.writestr(info_entry, path.read_bytes())
    with output.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    output.with_suffix(".zip.sha256").write_text(f"{digest}  {output.name}\n", encoding="utf-8")
    print(f"{platform} {version}: {output.name} {output.stat().st_size / 1024 / 1024:.1f} MiB / {digest}")
sys.exit(1 if problems else 0)
