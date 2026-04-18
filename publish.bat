@echo off
setlocal

echo ========================================
echo  סידור עבודה - Build ^& Publish
echo ========================================

set PROJECT=WorkSchedule\WorkSchedule.csproj
set OUT=publish\win-x64
set INSTALLER_OUT=installer\Output

echo.
echo [1/3] Cleaning previous publish output...
if exist "%OUT%" rmdir /s /q "%OUT%"

echo [2/3] Publishing self-contained single-file...
dotnet publish %PROJECT% ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishReadyToRun=true ^
  -o %OUT%

if errorlevel 1 (
    echo.
    echo ERROR: dotnet publish failed!
    exit /b 1
)

echo.
echo [3/3] Build complete.
echo Output: %OUT%\WorkSchedule.exe
echo.

REM If Inno Setup is installed, compile the installer automatically
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist %ISCC% (
    echo Compiling installer...
    %ISCC% installer\setup.iss
    if errorlevel 1 (
        echo ERROR: Inno Setup compilation failed!
        exit /b 1
    )
    echo Installer: %INSTALLER_OUT%\WorkSchedule-Setup-1.0.0.exe
) else (
    echo Inno Setup not found - skipping installer compilation.
    echo To build installer: install Inno Setup 6 and run ISCC.exe installer\setup.iss
)

echo.
echo Done!
endlocal
