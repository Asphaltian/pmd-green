set -e

APP="Green Rescue Team.app"
VERSION=$(dotnet msbuild src/PMDGreen -getProperty:Version)

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$1/." "$APP/Contents/MacOS/"
cp icons/pmd_green.icns "$APP/Contents/Resources/"
cp .github/macos/Info.plist "$APP/Contents/"
plutil -insert CFBundleShortVersionString -string "$VERSION" "$APP/Contents/Info.plist"
plutil -insert CFBundleVersion -string "$VERSION" "$APP/Contents/Info.plist"

codesign --force --deep --options runtime --entitlements .github/macos/entitlements.plist --sign - "$APP"
ditto -c -k --keepParent "$APP" pmd_green.zip
