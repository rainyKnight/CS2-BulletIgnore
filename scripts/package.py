#!/usr/bin/env python3
"""Package an already-built local draft. No deployment, remote access, or publication."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = ET.parse(ROOT / "Directory.Build.props").getroot().findtext("PropertyGroup/Version")
assert VERSION and all(c.isalnum() or c in ".-" for c in VERSION)
OUT = ROOT / "artifacts"
OUT.mkdir(exist_ok=True)
BUILD = ROOT / "src/BulletIgnore/bin/Release/net10.0"
DOCS = ["README.md", "README.zh-CN.md", "LICENSE", "LICENSE-NOTICES.md", "THIRD_PARTY_NOTICES.md"]
SUPPORT_ASSETS = [f"assets/sponsorship/{method}-{i}.jpg"
                  for method in ["wechat", "alipay"] for i in [1, 2]]
SOURCE_ROOT = DOCS + SUPPORT_ASSETS + [".editorconfig", ".gitignore", ".gitattributes", "Directory.Build.props", "Directory.Packages.props",
                      "global.json", "NuGet.Config", "CS2-BulletIgnore.slnx"]

def allowed_sources():
    result = {p: ROOT / p for p in SOURCE_ROOT}
    for folder in ["src", "tests", "scripts", "config", "docs", "LICENSES"]:
        for p in (ROOT / folder).rglob("*"):
            if p.is_file() and not any(x in {"bin", "obj", "__pycache__"} for x in p.relative_to(ROOT).parts):
                if p.suffix not in {".cs", ".csproj", ".py", ".md", ".json", ".txt"}:
                    raise RuntimeError(f"Unexpected source entry requires review: {p.relative_to(ROOT)}")
                result[p.relative_to(ROOT).as_posix()] = p
    return result

def archive(filename, entries):
    manifest = []
    with zipfile.ZipFile(OUT / filename, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for relative, p in sorted(entries.items()):
            assert not relative.startswith("/") and ".." not in Path(relative).parts
            data = p.read_bytes()
            info = zipfile.ZipInfo(relative, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (0o100644 << 16)
            z.writestr(info, data)
            manifest.append({"path": relative, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    # Verify actual archive bytes and every entry after writing.
    with zipfile.ZipFile(OUT / filename) as z:
        assert z.testzip() is None
        assert set(z.namelist()) == {x["path"] for x in manifest}
        for x in manifest:
            assert hashlib.sha256(z.read(x["path"])).hexdigest() == x["sha256"]
    (OUT / (filename + ".manifest.json")).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return hashlib.sha256((OUT / filename).read_bytes()).hexdigest()

def main():
    dll = BUILD / "BulletIgnore.dll"
    deps = BUILD / "BulletIgnore.deps.json"
    if not dll.exists() or not deps.exists():
        raise SystemExit("Build Release first: dotnet build CS2-BulletIgnore.slnx -c Release")
    sources = allowed_sources()
    for relative, p in sources.items():
        if relative.startswith("src/") and p.stat().st_mtime > dll.stat().st_mtime:
            raise SystemExit("Source is newer than DLL; rebuild before packaging.")
    assert not json.loads((ROOT / "config/bullet-ignore.json").read_text())["enabled"]
    plugin = {p: ROOT / p for p in DOCS + SUPPORT_ASSETS}
    for folder in ["docs", "LICENSES"]:
        plugin.update({p.relative_to(ROOT).as_posix(): p for p in (ROOT / folder).rglob("*") if p.is_file()})
    plugin.update({
        "sharp/modules/BulletIgnore/BulletIgnore.dll": dll,
        "sharp/modules/BulletIgnore/BulletIgnore.deps.json": deps,
        "sharp/configs/bullet-ignore.json": ROOT / "config/bullet-ignore.json"
    })
    packages = {
        f"CS2-BulletIgnore-{VERSION}-source.zip": sources,
        f"CS2-BulletIgnore-{VERSION}-plugin.zip": plugin
    }
    sums = []
    for name, entries in packages.items():
        digest = archive(name, entries)
        sums.append(f"{digest}  {name}")
        print(f"Verified {name}: {len(entries)} entries")
    (OUT / "SHA256SUMS").write_text("\n".join(sums) + "\n", encoding="utf-8")

if __name__ == "__main__":
    main()
