#!/bin/bash
set -euo pipefail
export COPYFILE_DISABLE=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/TeacherClient.Avalonia/TeacherClient.Avalonia.csproj"
SETUP_ROOT="$SCRIPT_DIR"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME="${RUNTIME:-osx-arm64}"
APP_NAME="${APP_NAME:-ClassCommander.app}"
PRODUCT_NAME="${PRODUCT_NAME:-ClassCommander}"
BUNDLE_ID="${BUNDLE_ID:-com.tarasmotuschuk.teacherclient.avalonia}"
DEFAULT_VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO_ROOT/Directory.Build.props" | head -n 1)"
VERSION="${VERSION:-${DEFAULT_VERSION:-1.0.0}}"
PUBLISH_DIR="$SETUP_ROOT/artifacts/publish"
APP_DIR="$SETUP_ROOT/artifacts/$APP_NAME"
PKG_DIR="$SETUP_ROOT/dist"
PKG_PATH="$PKG_DIR/ClassCommander.Setup.pkg"
ICON_PATH="$REPO_ROOT/Branding/ClassCommander-icon.icns"
STAGING_DIR="$(mktemp -d "${TMPDIR:-/tmp}/classcommander-pkg.XXXXXX")"

cleanup() {
  rm -rf "$STAGING_DIR"
}

trap cleanup EXIT

mkdir -p "$PUBLISH_DIR" "$PKG_DIR"
rm -rf "$PUBLISH_DIR" "$APP_DIR" "$PKG_PATH"
mkdir -p "$PUBLISH_DIR" "$PKG_DIR" "$STAGING_DIR"

echo "Publishing self-contained Avalonia client..."
dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$PUBLISH_DIR"

mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
ditto --norsrc "$PUBLISH_DIR" "$APP_DIR/Contents/MacOS"

FFMPEG_FRAMEWORKS_DIR="$APP_DIR/Contents/Frameworks/ffmpeg"
mkdir -p "$FFMPEG_FRAMEWORKS_DIR"

stage_ffmpeg_dylibs() {
  if [[ -n "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR:-}" && -d "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}" ]]; then
    echo "Staging FFmpeg dylibs from CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR=${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}"
    ditto --norsrc "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}/" "$FFMPEG_FRAMEWORKS_DIR"
    return
  fi

  # GitHub runners normally include Homebrew, but PATH can vary.
  if [[ -x "/opt/homebrew/bin/brew" ]]; then
    export PATH="/opt/homebrew/bin:$PATH"
  fi
  if [[ -x "/usr/local/bin/brew" ]]; then
    export PATH="/usr/local/bin:$PATH"
  fi
  if [[ -x "/usr/local/Homebrew/bin/brew" ]]; then
    export PATH="/usr/local/Homebrew/bin:$PATH"
  fi

  local brew_bin
  brew_bin="$(command -v brew 2>/dev/null || true)"
  if [[ -z "$brew_bin" ]]; then
    if [[ -x "/opt/homebrew/bin/brew" ]]; then brew_bin="/opt/homebrew/bin/brew"; fi
    if [[ -z "$brew_bin" && -x "/usr/local/bin/brew" ]]; then brew_bin="/usr/local/bin/brew"; fi
    if [[ -z "$brew_bin" && -x "/usr/local/Homebrew/bin/brew" ]]; then brew_bin="/usr/local/Homebrew/bin/brew"; fi
  fi

  if [[ -n "$brew_bin" ]]; then
    local prefix
    prefix="$("$brew_bin" --prefix ffmpeg 2>/dev/null || true)"
    if [[ -z "$prefix" ]]; then
      echo "Homebrew is available but ffmpeg is not installed. Installing ffmpeg..."
      "$brew_bin" install ffmpeg
      prefix="$("$brew_bin" --prefix ffmpeg 2>/dev/null || true)"
    fi
    if [[ -n "$prefix" && -d "$prefix/lib" ]]; then
      echo "Staging FFmpeg dylibs from Homebrew prefix: $prefix"
      # Copy the FFmpeg dylibs (plus swscale/swresample) that SIPSorcery loads.
      cp -f "$prefix/lib/libavcodec"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libavdevice"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libavfilter"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libavformat"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libavutil"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libswresample"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      cp -f "$prefix/lib/libswscale"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
      return
    fi
  fi

  # Fallback: download a prebuilt shared FFmpeg dylib bundle (no Homebrew required).
  if command -v curl >/dev/null 2>&1 && command -v python3 >/dev/null 2>&1; then
    echo "Attempting to download prebuilt FFmpeg dylibs (no Homebrew)..."
    local api_url asset_url dl_dir archive_path extract_dir json_path http_code
    api_url="https://api.github.com/repos/ColorsWind/FFmpeg-macOS/releases/latest"
    dl_dir="$SETUP_ROOT/artifacts/ffmpeg-macos"
    archive_path="$dl_dir/ffmpeg-macos.zip"
    extract_dir="$dl_dir/extract"
    json_path="$dl_dir/release.json"
    mkdir -p "$dl_dir"

    local curl_headers
    curl_headers=(
      -H "Accept: application/vnd.github+json"
      -H "User-Agent: ClassCommander-CI"
    )
    if [[ -n "${GITHUB_TOKEN:-}" ]]; then
      curl_headers+=(-H "Authorization: Bearer ${GITHUB_TOKEN}")
    fi

    http_code="$(curl -sS "${curl_headers[@]}" -o "$json_path" -w "%{http_code}" "$api_url" || true)"
    if [[ "$http_code" == "200" && -s "$json_path" ]]; then
      asset_url="$(python3 - "$json_path" <<'PY'
import json, sys
path=sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data=json.load(f)
assets=data.get("assets") or []
def score(name: str) -> int:
    n=name.lower()
    s=0
    if "universal" in n: s += 10
    if "shared" in n or "dylib" in n: s += 10
    if n.endswith(".zip"): s += 5
    return s
best=None
best_score=-1
for a in assets:
    name=a.get("name","")
    url=a.get("browser_download_url","")
    if not url:
        continue
    sc=score(name)
    if sc>best_score:
        best_score=sc
        best=url
if best:
    print(best)
PY
)" || true
    fi

    if [[ -n "$asset_url" ]]; then
      rm -rf "$extract_dir"
      mkdir -p "$extract_dir"
      echo "Downloading: $asset_url"
      curl -fL "$asset_url" -o "$archive_path"
      unzip -q -o "$archive_path" -d "$extract_dir"

      local found
      found=0
      while IFS= read -r -d '' f; do
        cp -f "$f" "$FFMPEG_FRAMEWORKS_DIR/"
        found=1
      done < <(find "$extract_dir" -type f -name '*.dylib' -print0 2>/dev/null || true)

      if [[ "$found" == "1" ]]; then
        echo "Staged FFmpeg dylibs from downloaded bundle."
        return
      fi
    fi
  fi

  echo "ERROR: FFmpeg dylibs not found. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR or ensure Homebrew+ffmpeg are available on the build machine." >&2
  exit 2
}

make_ffmpeg_relocatable() {
  local exe="$APP_DIR/Contents/MacOS/TeacherClient.Avalonia"
  if [[ ! -f "$exe" ]]; then
    return
  fi

  # Ensure the app can resolve @rpath to our bundled dylibs.
  install_name_tool -add_rpath "@executable_path/../Frameworks/ffmpeg" "$exe" 2>/dev/null || true

  # Rewrite dylib install names and internal dependencies to use @rpath.
  for lib in "$FFMPEG_FRAMEWORKS_DIR"/*.dylib; do
    [[ -f "$lib" ]] || continue
    base="$(basename "$lib")"
    install_name_tool -id "@rpath/$base" "$lib" 2>/dev/null || true

    # Point dependencies that are also bundled to @rpath.
    for dep in $(otool -L "$lib" | awk '{print $1}' | tail -n +2); do
      dep_base="$(basename "$dep")"
      if [[ -f "$FFMPEG_FRAMEWORKS_DIR/$dep_base" ]]; then
        install_name_tool -change "$dep" "@rpath/$dep_base" "$lib" 2>/dev/null || true
      fi
    done
  done
}

stage_ffmpeg_dylibs
make_ffmpeg_relocatable

INFO_PLIST_TEMPLATE="$SETUP_ROOT/Resources/Info.plist.template"
INFO_PLIST_PATH="$APP_DIR/Contents/Info.plist"

sed \
  -e "s|__BUNDLE_ID__|$BUNDLE_ID|g" \
  -e "s|__PRODUCT_NAME__|$PRODUCT_NAME|g" \
  -e "s|__EXECUTABLE_NAME__|TeacherClient.Avalonia|g" \
  -e "s|__VERSION__|$VERSION|g" \
  "$INFO_PLIST_TEMPLATE" > "$INFO_PLIST_PATH"

if [[ -f "$ICON_PATH" ]]; then
  ditto --norsrc "$ICON_PATH" "$APP_DIR/Contents/Resources/AppIcon.icns"
fi

echo -n "APPL????" > "$APP_DIR/Contents/PkgInfo"
chmod +x "$APP_DIR/Contents/MacOS/TeacherClient.Avalonia"
find "$APP_DIR" -name '._*' -delete
find "$APP_DIR" -name '.DS_Store' -delete
ditto --norsrc "$APP_DIR" "$STAGING_DIR/$APP_NAME"

echo "Building macOS installer package..."
pkgbuild \
  --root "$STAGING_DIR" \
  --install-location "/Applications" \
  --identifier "$BUNDLE_ID" \
  --version "$VERSION" \
  "$PKG_PATH"

echo
echo "Done."
echo "App bundle: $APP_DIR"
echo "Installer:   $PKG_PATH"
