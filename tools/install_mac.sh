#!/usr/bin/env bash
# Copies the built plugin into the Rhino plugin folder of the current user.
# Rhino reads that folder at startup, so restart Rhino after the copy.

set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLUGIN_PATH="${ROOT_DIR}/src/Import_SHP/bin/${CONFIGURATION}/net7.0/Import_SHP.rhp"
INSTALL_DIR="${HOME}/Library/Application Support/McNeel/Rhinoceros/MacPlugIns"

if [ ! -f "${PLUGIN_PATH}" ]; then
    echo "The plugin is not built. Run ./build.sh ${CONFIGURATION} first." >&2
    exit 1
fi

mkdir -p "${INSTALL_DIR}"
cp "${PLUGIN_PATH}" "${INSTALL_DIR}/"

echo "Installed: ${INSTALL_DIR}/Import_SHP.rhp"
echo "Restart Rhino 8 to load the plugin."
