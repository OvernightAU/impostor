@echo off
echo Choose the build configuration:
echo 1. Debug
echo 2. Release
set /p configChoice="Enter the number (1 or 2): "

if "%configChoice%"=="1" (
    set config=Debug
) else if "%configChoice%"=="2" (
    set config=Release
) else (
    echo Invalid choice. Defaulting to Release.
    set config=Release
)

dotnet publish -c %config% -p:PublishSingleFile=true --no-self-contained
pause