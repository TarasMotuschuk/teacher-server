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
VPXMD_DST="$APP_DIR/Contents/MacOS/vpxmd.dylib"
VPXMD_REPO_PATH="$SETUP_ROOT/Resources/vpxmd/$RUNTIME/vpxmd.dylib"

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

stage_vpxmd_dylib() {
  # SIPSorceryMedia.Encoders P/Invokes into a native library named "vpxmd".
  # On macOS we satisfy that dependency by shipping a libvpx dylib renamed to vpxmd.dylib.
  local src=""

  if [[ -f "$VPXMD_REPO_PATH" ]]; then
    src="$VPXMD_REPO_PATH"
  elif [[ -n "${CLASSCOMMANDER_VPXMD_MACOS_DYLIB:-}" && -f "${CLASSCOMMANDER_VPXMD_MACOS_DYLIB:-}" ]]; then
    src="$CLASSCOMMANDER_VPXMD_MACOS_DYLIB"
  elif [[ -n "${CLASSCOMMANDER_LIBVPX_MACOS_DYLIB:-}" && -f "${CLASSCOMMANDER_LIBVPX_MACOS_DYLIB:-}" ]]; then
    src="$CLASSCOMMANDER_LIBVPX_MACOS_DYLIB"
  fi

  if [[ -z "$src" ]]; then
    echo "ERROR: Missing libvpx for VP8 (vpxmd.dylib)." >&2
    echo "Provide one of:" >&2
    echo " - $VPXMD_REPO_PATH (recommended for CI; add file to repo manually)" >&2
    echo " - CLASSCOMMANDER_VPXMD_MACOS_DYLIB=/path/to/vpxmd.dylib" >&2
    echo " - CLASSCOMMANDER_LIBVPX_MACOS_DYLIB=/path/to/libvpx.dylib (it will be staged as vpxmd.dylib)" >&2
    exit 3
  fi

  echo "Staging VP8 native library: $src -> $VPXMD_DST"
  cp -f "$src" "$VPXMD_DST"
  chmod 644 "$VPXMD_DST" 2>/dev/null || true
}

stage_vpxmd_dylib


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
