set -e

ARCH=$(uname -m)
curl -sSfLO "https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-$ARCH.AppImage"
chmod a+x "linuxdeploy-$ARCH.AppImage"
"./linuxdeploy-$ARCH.AppImage" --appimage-extract > /dev/null
mv squashfs-root linuxdeploy

mkdir -p AppDir/usr/bin
cp -r "$1/." AppDir/usr/bin/

OUTPUT="pmd_green-$ARCH.AppImage" ./linuxdeploy/AppRun \
    --appdir AppDir \
    --executable AppDir/usr/bin/pmd_green \
    --desktop-file .github/linux/pmd_green.desktop \
    --icon-file icons/pmd_green.png \
    --output appimage
