#!/usr/bin/env bash
# Builds the shapefile import plugin and runs the unit tests.
#
# Usage:
#   ./build.sh            # build the plugin in Release and run the tests
#   ./build.sh Debug      # build in Debug

set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET="${DOTNET:-dotnet}"

if ! command -v "${DOTNET}" >/dev/null 2>&1; then
    if [ -x /run/current-system/sw/bin/dotnet ]; then
        DOTNET=/run/current-system/sw/bin/dotnet
    else
        echo "The .NET SDK is not on the PATH. Set the DOTNET variable to the dotnet executable." >&2
        exit 1
    fi
fi

echo "Building Import_SHP (${CONFIGURATION})"
"${DOTNET}" build "${ROOT_DIR}/src/Import_SHP/Import_SHP.csproj" -c "${CONFIGURATION}"

echo "Running the reader tests"
"${DOTNET}" test "${ROOT_DIR}/tests/Import_SHP.Tests/Import_SHP.Tests.csproj" -c "${CONFIGURATION}"

PLUGIN_PATH="${ROOT_DIR}/src/Import_SHP/bin/${CONFIGURATION}/net7.0/Import_SHP.rhp"
echo
echo "Plugin: ${PLUGIN_PATH}"
echo "Install it: ./tools/install_mac.sh, then restart Rhino 8."
