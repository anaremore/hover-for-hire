"""Zip existing players and preserve executable modes for macOS/Linux recipients."""
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import hashlib

root = Path(__file__).resolve().parent.parent / "Builds"
for platform in ("Windows", "macOS", "Linux"):
    source = root / platform
    if not source.is_dir():
        continue
    output = root / f"Hover-for-Hire-{platform}.zip"
    with ZipFile(output, "w", ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(source.rglob("*")):
            if not path.is_file() or any("DoNotShip" in part for part in path.parts):
                continue
            relative = path.relative_to(source).as_posix()
            info = ZipInfo(relative)
            info.create_system = 3
            executable = ("/Contents/MacOS/" in f"/{relative}" or path.suffix in (".x86_64", ".so", ".dylib", ".exe"))
            info.external_attr = (0o100755 if executable else 0o100644) << 16
            info.compress_type = ZIP_DEFLATED
            archive.writestr(info, path.read_bytes())
    with output.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    output.with_suffix(".zip.sha256").write_text(f"{digest}  {output.name}\n", encoding="utf-8")
    print(f"{platform}: {output.stat().st_size / 1024 / 1024:.1f} MiB / {digest}")
