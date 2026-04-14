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

  if command -v brew >/dev/null 2>&1; then
    local prefix
    prefix="$(brew --prefix ffmpeg 2>/dev/null || true)"
    if [[ -n "$prefix" && -d "$prefix/lib" ]]; then
      echo "Staging FFmpeg dylibs from Homebrew prefix: $prefix"
      # Copy FFmpeg libs + common codec deps that ffmpeg may link against.
      # We copy a broad set of dylibs and make them relocatable below.
      find "$prefix/lib" -maxdepth 1 -type f -name '*.dylib' -print0 | xargs -0 -I{} cp -f "{}" "$FFMPEG_FRAMEWORKS_DIR/" || true
      return
    fi
  fi

  echo "ERROR: FFmpeg dylibs not found. Set CLASSCOMMANDER_FFMPEG_MACOS_LIB_DIR or install ffmpeg via brew on the build machine." >&2
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
