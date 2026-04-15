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

# Create SONAME-style symlinks expected by FFmpeg.AutoGen (lib{name}.{major}.dylib).
# Some FFmpeg distributions only ship the "real name" (e.g. libavutil.59.39.100.dylib) and/or libavutil.dylib.
ensure_ffmpeg_soname_symlinks() {
  local lib_dir="$1"
  [[ -d "$lib_dir" ]] || return 0

  local -a libs
  libs=(
    "avutil:59"
    "avcodec:61"
    "avformat:61"
    "avdevice:61"
    "avfilter:10"
    "postproc:58"
    "swresample:5"
    "swscale:8"
  )

  local entry short major expected realname
  for entry in "${libs[@]}"; do
    short="${entry%%:*}"
    major="${entry##*:}"
    expected="lib${short}.${major}.dylib"

    if [[ -f "$lib_dir/$expected" ]]; then
      continue
    fi

    # Prefer "real name" that starts with the SONAME.
    realname="$(ls -1 "$lib_dir/lib${short}.${major}."*.dylib 2>/dev/null | head -1 || true)"
    if [[ -z "$realname" ]]; then
      continue
    fi

    ln -sf "$(basename "$realname")" "$lib_dir/$expected" 2>/dev/null || true
  done
}

# Download ColorsWind/FFmpeg-macOS (or similar) release asset; no Homebrew.
try_stage_ffmpeg_from_prebuilt_zip() {
  if ! command -v curl >/dev/null 2>&1 || ! command -v python3 >/dev/null 2>&1; then
    return 1
  fi

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
  if [[ "$http_code" != "200" || ! -s "$json_path" ]]; then
    return 1
  fi

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

  if [[ -z "$asset_url" ]]; then
    return 1
  fi

  rm -rf "$extract_dir"
  mkdir -p "$extract_dir"
  echo "Downloading FFmpeg dylibs: $asset_url"
  curl -fL "$asset_url" -o "$archive_path"
  unzip -q -o "$archive_path" -d "$extract_dir"

  # Do not flatten: dylibs use @loader_path/../lib/...; keep the directory that contains libavcodec.
  local first_codec
  first_codec="$(find "$extract_dir" -name 'libavcodec*.dylib' 2>/dev/null | head -1)"
  if [[ -z "$first_codec" ]]; then
    echo "ERROR: Prebuilt FFmpeg archive contains no libavcodec*.dylib." >&2
    return 1
  fi

  local lib_root
  lib_root="$(dirname "$first_codec")"
  # FFmpeg.AutoGen 7 expects FFmpeg 7 SONAMEs (libavutil.59.dylib, libavcodec.61.dylib, ...).
  # Some distributions don't ship SONAME names, only "real names" like libavutil.59.xx.yy.dylib, so accept either.
  if [[ ! -f "$lib_root/libavutil.59.dylib" && -z "$(ls -1 "$lib_root"/libavutil.59.*.dylib 2>/dev/null | head -1 || true)" ]]; then
    echo "WARNING: Prebuilt ZIP is not FFmpeg 7-compatible (no libavutil.59*.dylib in $lib_root). Skipping." >&2
    ls -la "$lib_root"/libavutil*.dylib 2>/dev/null || true
    return 1
  fi

  echo "Staging FFmpeg lib tree (no flatten): $lib_root -> Contents/Frameworks/ffmpeg/lib"
  rm -rf "$FFMPEG_FRAMEWORKS_DIR/lib"
  mkdir -p "$FFMPEG_FRAMEWORKS_DIR"
  ditto --norsrc "$lib_root" "$FFMPEG_FRAMEWORKS_DIR/lib"
  ensure_ffmpeg_soname_symlinks "$FFMPEG_FRAMEWORKS_DIR/lib"

  echo "Staged FFmpeg dylibs from downloaded bundle."
  return 0
}

stage_ffmpeg_dylibs() {
  if [[ -n "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR:-}" && -d "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}" ]]; then
    echo "Staging FFmpeg dylibs from CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR=${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}"
    ditto --norsrc "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}/" "$FFMPEG_FRAMEWORKS_DIR"
    if [[ -d "$FFMPEG_FRAMEWORKS_DIR/lib" ]]; then
      ensure_ffmpeg_soname_symlinks "$FFMPEG_FRAMEWORKS_DIR/lib"
    else
      ensure_ffmpeg_soname_symlinks "$FFMPEG_FRAMEWORKS_DIR"
    fi
    return
  fi

  if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
    echo "GITHUB_ACTIONS=true: staging FFmpeg from prebuilt bundle only."
    if try_stage_ffmpeg_from_prebuilt_zip; then
      return
    fi
    echo "ERROR: Could not stage FFmpeg on CI. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR (FFmpeg 7 dylibs) or update the prebuilt ZIP source to an FFmpeg 7 bundle." >&2
    exit 2
  fi

  if try_stage_ffmpeg_from_prebuilt_zip; then
    return
  fi

  echo "ERROR: FFmpeg dylibs not found. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR or ensure curl/python3 can download a FFmpeg 7-compatible prebuilt bundle." >&2
  exit 2
}

# FFmpeg.AutoGen 7 (via SIPSorceryMedia.FFmpeg) uses fixed SONAMEs — see TeacherClient.Avalonia FfmpegBootstrap.MacFfmpegAutogen7Dylibs.
verify_ffmpeg_autogen7_dylibs() {
  local lib_dir="$FFMPEG_FRAMEWORKS_DIR/lib"
  if [[ ! -d "$lib_dir" ]]; then
    lib_dir="$FFMPEG_FRAMEWORKS_DIR"
  fi
  ensure_ffmpeg_soname_symlinks "$lib_dir"
  local missing=0
  local f
  for f in \
    libavutil.59.dylib \
    libavcodec.61.dylib \
    libavformat.61.dylib \
    libavdevice.61.dylib \
    libavfilter.10.dylib \
    libpostproc.58.dylib \
    libswresample.5.dylib \
    libswscale.8.dylib; do
    if [[ ! -f "$lib_dir/$f" ]]; then
      echo "ERROR: Bundled FFmpeg must be FFmpeg 7-compatible (FFmpeg.AutoGen 7). Missing: $lib_dir/$f" >&2
      missing=1
    fi
  done
  if [[ "$missing" -ne 0 ]]; then
    echo "Found libavutil matches:" >&2
    ls -la "$lib_dir"/libavutil*.dylib 2>/dev/null || echo "(none)" >&2
    exit 2
  fi
}

make_ffmpeg_relocatable() {
  local exe="$APP_DIR/Contents/MacOS/TeacherClient.Avalonia"
  if [[ ! -f "$exe" ]]; then
    return
  fi

  install_name_tool -add_rpath "@executable_path/../Frameworks/ffmpeg" "$exe" 2>/dev/null || true
  install_name_tool -add_rpath "@executable_path/../Frameworks/ffmpeg/lib" "$exe" 2>/dev/null || true

  # SIPSorcery calls avdevice_register_all(); unresolved dylibs -> DllNotFoundException.
  # Process every dylib under Frameworks/ffmpeg (flat Homebrew copy or prebuilt lib/ tree).
  local pass
  for pass in 1 2 3 4 5 6; do
    local lib
    while IFS= read -r -d '' lib; do
      [[ -f "$lib" ]] || continue
      local lib_dir
      lib_dir="$(dirname "$lib")"

      if [[ "$pass" == "1" ]]; then
        install_name_tool -add_rpath "@loader_path/." "$lib" 2>/dev/null || true
        install_name_tool -add_rpath "@executable_path/../Frameworks/ffmpeg" "$lib" 2>/dev/null || true
        install_name_tool -add_rpath "@executable_path/../Frameworks/ffmpeg/lib" "$lib" 2>/dev/null || true
      fi

      local base
      base="$(basename "$lib")"
      install_name_tool -id "@loader_path/$base" "$lib" 2>/dev/null || true

      local dep
      while IFS= read -r dep; do
        [[ -z "$dep" ]] && continue
        case "$dep" in
          /System/*|/usr/lib/*)
            continue
            ;;
        esac

        local dep_base
        dep_base="$(basename "$dep")"

        if [[ "$dep" == "@loader_path/$dep_base" ]]; then
          continue
        fi

        local resolved
        resolved=""
        if [[ -f "$lib_dir/$dep_base" ]]; then
          resolved="$lib_dir/$dep_base"
        elif [[ -f "$FFMPEG_FRAMEWORKS_DIR/lib/$dep_base" ]]; then
          resolved="$FFMPEG_FRAMEWORKS_DIR/lib/$dep_base"
        elif [[ -f "$FFMPEG_FRAMEWORKS_DIR/$dep_base" ]]; then
          resolved="$FFMPEG_FRAMEWORKS_DIR/$dep_base"
        fi

        if [[ -z "$resolved" ]]; then
          continue
        fi

        install_name_tool -change "$dep" "@loader_path/$dep_base" "$lib" 2>/dev/null || true
      done < <(otool -L "$lib" | tail -n +2 | awk '{print $1}')
    done < <(find "$FFMPEG_FRAMEWORKS_DIR" -type f -name '*.dylib' -print0 2>/dev/null || true)
  done
}

codesign_app_bundle() {
  if ! command -v codesign >/dev/null 2>&1; then
    echo "WARNING: codesign is not available; skipping app signing."
    return
  fi

  echo "Codesigning app bundle (ad-hoc)..."

  # Self-contained .NET publish includes multiple nested Mach-O binaries under Contents/MacOS.
  # Sign the whole bundle deeply so all nested code gets a consistent signature.
  codesign --force --deep --sign - --timestamp=none "$APP_DIR" >/dev/null 2>&1 || {
    echo "ERROR: codesign failed for $APP_DIR" >&2
    codesign --force --deep --sign - --timestamp=none --verbose=4 "$APP_DIR" || true
    exit 3
  }

  # Fail early in CI if the bundle is still invalid.
  codesign --verify --deep --strict "$APP_DIR" >/dev/null 2>&1 || {
    echo "ERROR: codesign verification failed for $APP_DIR" >&2
    codesign --verify --deep --strict --verbose=4 "$APP_DIR" || true
    exit 3
  }
}

stage_ffmpeg_dylibs
verify_ffmpeg_autogen7_dylibs
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
# CI downloads can leave com.apple.quarantine on dylibs; strip before codesign so dlopen works on user machines.
xattr -cr "$APP_DIR/Contents/Frameworks" 2>/dev/null || true
codesign_app_bundle
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
