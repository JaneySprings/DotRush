#!/usr/bin/env bash

if [ -z "$1" ] || [ -z "$2" ]; then
    echo '{"isSuccess":false,"message":"Invocation error!"}'
    exit 1
fi

VERSION="$1"
RUNTIME_ID="$2"
SDK_DIR="$(pwd)/../Sdk"
ARCHIVE_NAME="dotnet-sdk-$VERSION-$RUNTIME_ID.tar.gz"
DOWNLOAD_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/$VERSION/$ARCHIVE_NAME"

ARCHIVE_PATH="$(pwd)/$ARCHIVE_NAME"

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
