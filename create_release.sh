# REMEMBER TO UPDATE MOD VERSION IN AssemblyInfo.cs, AnyPortal.cs, AND manifest.json!!!
# Create a release zip suitable for Nexus and Thunderstore

set -x
set -e

RELEASE_DIR="AnyPortalRelease"

rm -rf $RELEASE_DIR

mkdir -p $RELEASE_DIR

# Game lib and AssetBundle
cp bin/Debug/AnyPortal.dll $RELEASE_DIR/

# Other stuff...
cp icon.png $RELEASE_DIR
cp manifest.json $RELEASE_DIR
cp README.md $RELEASE_DIR

rm -f AnyPortal.zip
cd $RELEASE_DIR; zip -r ../AnyPortal.zip *