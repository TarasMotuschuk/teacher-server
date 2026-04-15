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
  # ColorsWind/FFmpeg-macOS "latest" is FFmpeg 5 (libavutil.57). FFmpeg.AutoGen 7 needs FFmpeg 7 (libavutil.59).
  if [[ ! -f "$lib_root/libavutil.59.dylib" ]]; then
    echo "WARNING: Prebuilt ZIP is not FFmpeg.AutoGen-7-compatible (no libavutil.59.dylib in $lib_root). Skipping." >&2
    ls -la "$lib_root"/libavutil*.dylib 2>/dev/null || true
    return 1
  fi

  echo "Staging FFmpeg lib tree (no flatten): $lib_root -> Contents/Frameworks/ffmpeg/lib"
  rm -rf "$FFMPEG_FRAMEWORKS_DIR/lib"
  mkdir -p "$FFMPEG_FRAMEWORKS_DIR"
  ditto --norsrc "$lib_root" "$FFMPEG_FRAMEWORKS_DIR/lib"

  echo "Staged FFmpeg dylibs from downloaded bundle."
  return 0
}

# Returns 0 if dylibs were copied from a Homebrew ffmpeg keg, 1 otherwise.
stage_ffmpeg_dylibs_from_homebrew() {
  # GitHub Actions (and other non-login shells): Homebrew exists but is not on PATH until shellenv runs.
  if [[ -x /opt/homebrew/bin/brew ]]; then
    eval "$(/opt/homebrew/bin/brew shellenv)"
  elif [[ -x /usr/local/bin/brew ]]; then
    eval "$(/usr/local/bin/brew shellenv)"
  elif [[ -x /usr/local/Homebrew/bin/brew ]]; then
    eval "$(/usr/local/Homebrew/bin/brew shellenv)"
  fi

  local brew_bin
  brew_bin="$(command -v brew 2>/dev/null || true)"
  if [[ -z "$brew_bin" ]]; then
    if [[ -x "/opt/homebrew/bin/brew" ]]; then brew_bin="/opt/homebrew/bin/brew"; fi
    if [[ -z "$brew_bin" && -x "/usr/local/bin/brew" ]]; then brew_bin="/usr/local/bin/brew"; fi
    if [[ -z "$brew_bin" && -x "/usr/local/Homebrew/bin/brew" ]]; then brew_bin="/usr/local/Homebrew/bin/brew"; fi
  fi

  if [[ -z "$brew_bin" ]]; then
    echo "ERROR: Homebrew not found (no brew in PATH and no standard locations)." >&2
    return 1
  fi

  local prefix
  prefix="$("$brew_bin" --prefix ffmpeg 2>/dev/null || true)"
  if [[ -z "$prefix" || ! -d "$prefix/lib" ]]; then
    echo "Installing ffmpeg via Homebrew..."
    export HOMEBREW_NO_AUTO_UPDATE="${HOMEBREW_NO_AUTO_UPDATE:-1}"
    if ! "$brew_bin" install ffmpeg; then
      echo "ERROR: brew install ffmpeg failed." >&2
      return 1
    fi
    prefix="$("$brew_bin" --prefix ffmpeg 2>/dev/null || true)"
  fi
  if [[ -z "$prefix" || ! -d "$prefix/lib" ]]; then
    echo "ERROR: ffmpeg keg has no lib directory at prefix=$prefix" >&2
    return 1
  fi

  echo "Staging FFmpeg dylibs from Homebrew prefix: $prefix"
  local brew_prefix
  brew_prefix="$("$brew_bin" --prefix 2>/dev/null || true)"
  if [[ -z "$brew_prefix" ]]; then
    if [[ -d "/opt/homebrew" ]]; then brew_prefix="/opt/homebrew"; fi
    if [[ -z "$brew_prefix" && -d "/usr/local" ]]; then brew_prefix="/usr/local"; fi
  fi

  mkdir -p "$FFMPEG_FRAMEWORKS_DIR"
  cp -f "$prefix/lib/libavcodec"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libavdevice"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libavfilter"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libavformat"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libavutil"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libswresample"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
  cp -f "$prefix/lib/libswscale"*.dylib "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true

  local changed
  changed=1
  while [[ "$changed" == "1" ]]; do
    changed=0
    for lib in "$FFMPEG_FRAMEWORKS_DIR"/*.dylib; do
      [[ -f "$lib" ]] || continue
      for dep in $(otool -L "$lib" | awk '{print $1}' | tail -n +2); do
        case "$dep" in
          @*|/System/*|/usr/lib/*)
            continue
            ;;
        esac

        local dep_base
        dep_base="$(basename "$dep")"
        if [[ -f "$FFMPEG_FRAMEWORKS_DIR/$dep_base" ]]; then
          continue
        fi

        if [[ -f "$dep" && "$dep" == *.dylib && -n "$brew_prefix" && "$dep" == "$brew_prefix"* ]]; then
          cp -f "$dep" "$FFMPEG_FRAMEWORKS_DIR/" 2>/dev/null || true
          changed=1
        fi
      done
    done
  done

  if [[ ! -f "$FFMPEG_FRAMEWORKS_DIR/libavutil.59.dylib" ]]; then
    echo "ERROR: After Homebrew staging, libavutil.59.dylib is missing (FFmpeg.AutoGen 7 needs FFmpeg 7). Contents:" >&2
    ls -la "$FFMPEG_FRAMEWORKS_DIR" >&2 || true
    return 1
  fi

  return 0
}

stage_ffmpeg_dylibs() {
  if [[ -n "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR:-}" && -d "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}" ]]; then
    echo "Staging FFmpeg dylibs from CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR=${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}"
    ditto --norsrc "${CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR}/" "$FFMPEG_FRAMEWORKS_DIR"
    return
  fi

  # GitHub Actions: try the GitHub ZIP first; ColorsWind/FFmpeg-macOS only publishes FFmpeg 5, so we fall back to Homebrew.
  if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
    echo "GITHUB_ACTIONS=true: staging FFmpeg (prebuilt ZIP if FFmpeg 7-compatible, else Homebrew)."
    if try_stage_ffmpeg_from_prebuilt_zip; then
      return
    fi
    echo "Prebuilt bundle unavailable or wrong FFmpeg major; using Homebrew ffmpeg."
    if stage_ffmpeg_dylibs_from_homebrew; then
      return
    fi
    echo "ERROR: Could not stage FFmpeg on CI. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR, or ensure Homebrew can install ffmpeg." >&2
    exit 2
  fi

  if stage_ffmpeg_dylibs_from_homebrew; then
    return
  fi

  if try_stage_ffmpeg_from_prebuilt_zip; then
    return
  fi

  echo "ERROR: FFmpeg dylibs not found. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR, install Homebrew ffmpeg, or ensure curl/python3 can download a FFmpeg 7-compatible prebuilt bundle." >&2
  exit 2
}

# FFmpeg.AutoGen 7 (via SIPSorceryMedia.FFmpeg) uses fixed SONAMEs — see TeacherClient.Avalonia FfmpegBootstrap.MacFfmpegAutogen7Dylibs.
verify_ffmpeg_autogen7_dylibs() {
  local lib_dir="$FFMPEG_FRAMEWORKS_DIR/lib"
  if [[ ! -d "$lib_dir" ]]; then
    lib_dir="$FFMPEG_FRAMEWORKS_DIR"
  fi
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
