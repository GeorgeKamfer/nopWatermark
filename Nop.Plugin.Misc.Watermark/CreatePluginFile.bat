@echo off
setlocal enabledelayedexpansion

REM Define the source directories
set "source_dir1=C:\Personal\Development\Nop.Plugin.Misc.Watermark\Nop.Plugin.Misc.Watermark\bin\Release\net8.0"
set "source_dir2=C:\Personal\Development\Nop.Plugin.Misc.Watermark\Nop.Plugin.Misc.Watermark"

REM Define the files to include in the zip
set "file1=Nop.Plugin.Misc.Watermark.dll"
set "file2=Nop.Plugin.Misc.Watermark.deps.json"
set "file3=Nop.Plugin.Misc.Watermark.pdb"
set "file4=plugin.json"
set "file5=logo.png"

REM Define the temporary directory
set "temp_dir=%source_dir2%\temp"

REM Clean up any existing temp directory
if exist "%temp_dir%" rmdir /s /q "%temp_dir%"

REM Create the temporary directory
mkdir "%temp_dir%"

REM Create the Misc.Watermark folder inside the temp directory
mkdir "%temp_dir%\Misc.Watermark"

REM Copy files to the Misc.Watermark folder
xcopy "%source_dir1%\%file1%" "%temp_dir%\Misc.Watermark\" /Y
xcopy "%source_dir1%\%file2%" "%temp_dir%\Misc.Watermark\" /Y
xcopy "%source_dir1%\%file3%" "%temp_dir%\Misc.Watermark\" /Y
xcopy "%source_dir2%\%file4%" "%temp_dir%\Misc.Watermark\" /Y
xcopy "%source_dir2%\Content\Images\%file5%" "%temp_dir%\Misc.Watermark\" /Y

REM Copy additional folders
xcopy /E /I "%source_dir2%\Views" "%temp_dir%\Misc.Watermark\Views" /Y
xcopy /E /I "%source_dir2%\Resources" "%temp_dir%\Misc.Watermark\Resources" /Y
xcopy /E /I "%source_dir2%\Content" "%temp_dir%\Misc.Watermark\Content" /Y
xcopy /E /I "%source_dir2%\Fonts" "%temp_dir%\Misc.Watermark\Fonts" /Y
xcopy /E /I "%source_dir2%\Script" "%temp_dir%\Misc.Watermark\Script" /Y

REM Extract version from plugin.json (robust parsing)
set "VERSION=1.0.0"
for /f "tokens=2 delims=:," %%i in ('findstr /r "\"Version\"" "%temp_dir%\Misc.Watermark\plugin.json"') do (
    set "tempver=%%i"
    set "tempver=!tempver:"=!"
    set "tempver=!tempver: =!"
    set "tempver=!tempver:^}=!"
    if not "!tempver!"=="" set "VERSION=!tempver!"
)

REM Define versioned output zip
set "output_zip=%source_dir2%\Plugin\Nop.Plugin.Misc.Watermark-v%VERSION%.zip"

REM Delete old versioned zips
del /f /q "%source_dir2%\Plugin\Nop.Plugin.Misc.Watermark-v*.zip" 2>nul

REM Create versioned zip
7z a -tzip "%output_zip%" "%temp_dir%\*" >nul 2>&1

if exist "%output_zip%" (
    echo.
    echo SUCCESS: Zip created - %output_zip%
    echo Version used: %VERSION%
    echo.
) else (
    echo.
    echo ERROR: Failed to create zip file
    echo.
)

REM Clean up: delete the temp directory
rmdir /s /q "%temp_dir%"

pause
