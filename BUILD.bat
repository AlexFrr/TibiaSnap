@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Could not find the C# compiler that comes with Windows .NET Framework 4.
    pause
    exit /b 1
)

echo Building TibiaSnap.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /out:TibiaSnap.exe /win32icon:TibiaSnap.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll TibiaSnap.cs
if errorlevel 1 (
    echo.
    echo Build failed - send the error above to Claude.
    pause
    exit /b 1
)

echo.
echo Done! TibiaSnap.exe is ready. You can move it anywhere and delete the other files.
pause
