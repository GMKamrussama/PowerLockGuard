@echo off
set CSC_64=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set CSC_32=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

set CSC=%CSC_64%
if not exist "%CSC%" set CSC=%CSC_32%

echo Compiling PowerLockGuard v2.0...
"%CSC%" /target:winexe /optimize+ /win32icon:app.ico /out:PowerLockGuard.exe /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll PowerLockGuard.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] PowerLockGuard.exe compiled successfully!
) else (
    echo.
    echo [FAILED] Compilation failed!
)
pause
