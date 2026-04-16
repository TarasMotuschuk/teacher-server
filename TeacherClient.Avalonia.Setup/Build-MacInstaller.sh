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
SIGNING_MODE="${SIGNING_MODE:-adhoc}"
APP_SIGN_IDENTITY="${APP_SIGN_IDENTITY:-Apple Development}"
PKG_SIGN_IDENTITY="${PKG_SIGN_IDENTITY:-}"
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

codesign_app_bundle() {
  if ! command -v codesign >/dev/null 2>&1; then
    echo "WARNING: codesign is not available; skipping app signing."
    return
  fi

  case "$SIGNING_MODE" in
    none)
      echo "Skipping app signing (SIGNING_MODE=none)."
      return
      ;;
    adhoc)
      echo "Codesigning app bundle (ad-hoc)..."
      codesign --force --deep --sign - --timestamp=none "$APP_DIR" >/dev/null 2>&1 || {
        echo "ERROR: codesign failed for $APP_DIR" >&2
        codesign --force --deep --sign - --timestamp=none --verbose=4 "$APP_DIR" || true
        exit 3
      }
      ;;
    apple-development)
      echo "Codesigning app bundle (Apple Development: $APP_SIGN_IDENTITY)..."
      codesign --force --deep --options runtime --timestamp --sign "$APP_SIGN_IDENTITY" "$APP_DIR" >/dev/null 2>&1 || {
        echo "ERROR: codesign failed for $APP_DIR" >&2
        codesign --force --deep --options runtime --timestamp --sign "$APP_SIGN_IDENTITY" --verbose=4 "$APP_DIR" || true
        exit 3
      }
      ;;
    *)
      echo "ERROR: Unknown SIGNING_MODE '$SIGNING_MODE'. Use none, adhoc, or apple-development." >&2
      exit 3
      ;;
  esac

  # Self-contained .NET publish includes multiple nested Mach-O binaries under Contents/MacOS.
  # Sign the whole bundle deeply so all nested code gets a consistent signature.
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

if [[ "$SIGNING_MODE" == "apple-development" && -n "$PKG_SIGN_IDENTITY" ]]; then
  if ! command -v productsign >/dev/null 2>&1; then
    echo "WARNING: productsign is not available; leaving installer unsigned."
  else
    SIGNED_PKG_PATH="$PKG_DIR/ClassCommander.Setup.signed.pkg"
    echo "Signing installer package ($PKG_SIGN_IDENTITY)..."
    productsign --sign "$PKG_SIGN_IDENTITY" "$PKG_PATH" "$SIGNED_PKG_PATH"
    mv "$SIGNED_PKG_PATH" "$PKG_PATH"
  fi
fi

echo
echo "Done."
echo "App bundle: $APP_DIR"
echo "Installer:   $PKG_PATH"
