#!/bin/sh

SCRIPTPATH="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
sh "$SCRIPTPATH/build-tools.sh"

BINARIES_PATH="$SCRIPTPATH/../src/ImageProcessor/bin/Debug/net9.0/"
BINARY_FILE="$BINARIES_PATH/ImageProcessor"

echo "Processing new images..."
"$BINARY_FILE" process-images