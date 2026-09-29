#!/usr/bin/env bash
# Build a self-contained DoorSim release package for one runtime identifier.
#
#   scripts/package.sh <rid> <version> [--deb]
#
#   rid      win-x64 | osx-arm64 | linux-x64 | linux-arm64
#   version  semver without the leading "v", e.g. 1.2.3 or 1.2.3-rc1
#   --deb    also build doorsim_<version>_<arch>.deb (linux-* only; needs nfpm on PATH)
#
# Output lands in dist/: doorsim-<version>-<rid>.zip (Windows) or .tar.gz (macOS/Linux), plus the
# .deb when requested. Used by .github/workflows/release.yml; runs the same locally.
set -euo pipefail

if [ $# -lt 2 ]; then
    sed -n '2,11p' "$0" >&2
    exit 2
fi

rid=$1
version=$2
build_deb=false
if [ "${3:-}" = "--deb" ]; then
    build_deb=true
fi

repo_root=$(cd "$(dirname "$0")/.." && pwd)
dist="$repo_root/dist"
name="doorsim-$version-$rid"
publish_dir="$dist/stage/publish-$rid"
archive_dir="$dist/stage/$name"

rm -rf "$publish_dir" "$archive_dir"
mkdir -p "$dist"

echo "==> Publishing DoorSim $version for $rid (self-contained)"
dotnet publish "$repo_root/source/src/DoorSim/DoorSim.csproj" \
    -c Release \
    -r "$rid" \
    --self-contained \
    -p:Version="$version" \
    -o "$publish_dir"

# --- Archive ------------------------------------------------------------------------------------
mkdir -p "$archive_dir"
cp -R "$publish_dir/." "$archive_dir/"
cp "$repo_root/README.md" "$repo_root/LICENSE" "$archive_dir/"

case "$rid" in
    linux-*)
        # Pi / RS-485 helpers for tarball installs (the .deb installs these itself).
        cp "$repo_root/scripts/99-waveshare485.rules" \
           "$repo_root/scripts/setup-pi.sh" \
           "$repo_root/scripts/wait-for-rs485.sh" \
           "$archive_dir/"
        ;;
esac

case "$rid" in
    win-*)
        archive="$dist/$name.zip"
        rm -f "$archive"
        if command -v zip >/dev/null 2>&1; then
            (cd "$dist/stage" && zip -qr "$archive" "$name")
        else
            # Git Bash on Windows has no zip; PowerShell needs native (not /f/...) paths.
            src=$archive_dir
            dst=$archive
            if command -v cygpath >/dev/null 2>&1; then
                src=$(cygpath -w "$src")
                dst=$(cygpath -w "$dst")
            fi
            pwsh -NoProfile -Command "Compress-Archive -Path '$src' -DestinationPath '$dst'"
        fi
        ;;
    *)
        archive="$dist/$name.tar.gz"
        tar -C "$dist/stage" -czf "$archive" "$name"
        ;;
esac
echo "==> Wrote $archive"

# --- Debian package -----------------------------------------------------------------------------
if [ "$build_deb" = true ]; then
    case "$rid" in
        linux-x64) deb_arch=amd64 ;;
        linux-arm64) deb_arch=arm64 ;;
        *)
            echo "--deb is only supported for linux-x64 and linux-arm64 (got $rid)" >&2
            exit 2
            ;;
    esac

    # nfpm.yaml reads the app from this fixed path.
    rm -rf "$repo_root/packaging/.stage"
    mkdir -p "$repo_root/packaging/.stage/app"
    cp -R "$publish_dir/." "$repo_root/packaging/.stage/app/"

    echo "==> Building .deb ($deb_arch)"
    (cd "$repo_root" && VERSION="$version" DEB_ARCH="$deb_arch" \
        nfpm package --config packaging/nfpm.yaml --packager deb --target "$dist/")
fi
