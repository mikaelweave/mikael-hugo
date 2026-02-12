#!/bin/sh

SCRIPTPATH="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
BINARIES_PATH="$SCRIPTPATH/../src/ImageProcessor/bin/Debug/net9.0/"
BINARY_FILE="$BINARIES_PATH/ImageProcessor"

if [ ! -d "$BINARIES_PATH" ] || [ ! -f "$BINARY_FILE" ]; then
    echo "Binaries not found or directory missing. Building..."
    dotnet build "$SCRIPTPATH/../src/ImageProcessor/ImageProcessor.csproj"
else
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS commands
        WEEK_AGO=$(date -v-1w +%s)
        FILE_DATE=$(stat -f "%m" "$BINARY_FILE")
    else
        # Linux commands
        WEEK_AGO=$(date -d "now - 1 week" +%s)
        FILE_DATE=$(date -r "$BINARY_FILE" +%s)
    fi

    if [ $FILE_DATE -lt $WEEK_AGO ]; then
        echo "Binary is older than one week. Rebuilding..."
        dotnet build "$SCRIPTPATH/../src/ImageProcessor/ImageProcessor.csproj"
    else
        echo "Binary is up-to-date. Skipping build."
    fi
fi