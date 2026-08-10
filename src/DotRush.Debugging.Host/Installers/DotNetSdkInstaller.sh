#!/usr/bin/env bash

if [ -z "$1" ] || [ -z "$2" ] || [ -z "$3" ]; then
    echo '{"isSuccess":false,"message":"Invocation error!"}'
    exit 1
fi

VERSION="$1"
PLATFORM="$2"
ARCH="$3"

case "$PLATFORM" in
    darwin) PLATFORM="osx" ;;
    win32) PLATFORM="win" ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

RUNTIME_ID="$PLATFORM-$ARCH"
SDK_DIR="$(dirname "$SCRIPT_DIR")/Sdk"
ARCHIVE_NAME="dotnet-sdk-$VERSION-$RUNTIME_ID.tar.gz"
DOWNLOAD_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/$VERSION/$ARCHIVE_NAME"

ARCHIVE_PATH="$SCRIPT_DIR/$ARCHIVE_NAME"

if ! curl -fsSL "$DOWNLOAD_URL" -o "$ARCHIVE_PATH"; then
    echo "{\"isSuccess\":false,\"message\":\"Failed to download $DOWNLOAD_URL\"}"
    exit 1
fi

rm -rf "$SDK_DIR"
mkdir -p "$SDK_DIR"
if ! tar -xzf "$ARCHIVE_PATH" -C "$SDK_DIR"; then
    echo "{\"isSuccess\":false,\"message\":\"Failed to extract $ARCHIVE_PATH\"}"
    rm -f "$ARCHIVE_PATH"
    exit 1
fi

rm -f "$ARCHIVE_PATH"
echo '{"isSuccess":true,"message":null}'
