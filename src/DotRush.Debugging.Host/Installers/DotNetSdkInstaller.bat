@echo off
setlocal

if "%~1"=="" goto :error
if "%~2"=="" goto :error
if "%~3"=="" goto :error

set "VERSION=%~1"
set "PLATFORM=%~2"
set "ARCH=%~3"

if /i "%PLATFORM%"=="darwin" set "PLATFORM=osx"
if /i "%PLATFORM%"=="win32" set "PLATFORM=win"

for %%I in ("%~dp0..") do set "PARENT_DIR=%%~fI"

set "RUNTIME_ID=%PLATFORM%-%ARCH%"
set "SDK_DIR=%PARENT_DIR%\Sdk"
set "ARCHIVE_NAME=dotnet-sdk-%VERSION%-%RUNTIME_ID%.zip"
set "DOWNLOAD_URL=https://builds.dotnet.microsoft.com/dotnet/Sdk/%VERSION%/%ARCHIVE_NAME%"
set "ARCHIVE_PATH=%~dp0%ARCHIVE_NAME%"

curl -fsSL "%DOWNLOAD_URL%" -o "%ARCHIVE_PATH%"
if errorlevel 1 (
    echo {"isSuccess":false,"message":"Failed to download %DOWNLOAD_URL%"}
    exit /b 1
)

if exist "%SDK_DIR%" rmdir /s /q "%SDK_DIR%"
mkdir "%SDK_DIR%"
tar -xf "%ARCHIVE_PATH%" -C "%SDK_DIR%"
if errorlevel 1 (
    echo {"isSuccess":false,"message":"Failed to extract %ARCHIVE_PATH:\=\\%"}
    del /q "%ARCHIVE_PATH%"
    exit /b 1
)

del /q "%ARCHIVE_PATH%"
echo {"isSuccess":true,"message":null}
exit /b 0

:error
echo {"isSuccess":false,"message":"Invocation error!"}
exit /b 1
