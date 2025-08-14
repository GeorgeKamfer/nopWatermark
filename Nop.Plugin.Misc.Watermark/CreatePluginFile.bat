@echo off
setlocal

REM Define the source directories
set "source_dir1=C:\Personal\Development\Nop.Plugin.Misc.Watermark\Nop.Plugin.Misc.Watermark\bin\Debug\net8.0"
set "source_dir2=C:\Personal\Development\Nop.Plugin.Misc.Watermark\Nop.Plugin.Misc.Watermark"

REM Define the files to include in the zip
set "file1=Nop.Plugin.Misc.Watermark.dll"
set "file2=Nop.Plugin.Misc.Watermark.deps.json"
set "file3=Nop.Plugin.Misc.Watermark.pdb"
set "file4=plugin.json"
set "file5=logo.png"

REM Define the temporary directory
set "temp_dir=%source_dir2%\temp"

REM Create the temporary directory
mkdir "%temp_dir%"

    REM Create the Misc.BlanksAreUs folder inside the temp directory
    mkdir "%temp_dir%\Misc.Watermark"

    REM Copy files to the Misc.Watermark folder
    xcopy "%source_dir1%\%file1%" "%temp_dir%\Misc.Watermark"
    xcopy "%source_dir1%\%file2%" "%temp_dir%\Misc.Watermark"
    xcopy "%source_dir1%\%file3%" "%temp_dir%\Misc.Watermark"
    xcopy "%source_dir2%\%file4%" "%temp_dir%\Misc.Watermark"
    xcopy "%source_dir2%\Content\Images\%file5%" "%temp_dir%\Misc.Watermark"

	xcopy /E /I "%source_dir2%\Views" "%temp_dir%\Misc.Watermark\Views"
    xcopy /E /I "%source_dir2%\Resources" "%temp_dir%\Misc.Watermark\Resources"
    xcopy /E /I "%source_dir2%\Content" "%temp_dir%\Misc.Watermark\Content"
    xcopy /E /I "%source_dir2%\Fonts" "%temp_dir%\Misc.Watermark\Fonts"
    xcopy /E /I "%source_dir2%\Script" "%temp_dir%\Misc.Watermark\Script"

    REM Define the output zip file path
    set "output_zip=%source_dir2%\Plugin\Nop.Plugin.Misc.Watermark.zip"

	REM Delete old zip file
	del /f /q "%source_dir2%\Plugin\Nop.Plugin.Misc.Watermark.zip"
pause
    REM Create the zip file
    7z a -tzip "%output_zip%" "%temp_dir%\*"

    echo Zip file created at %output_zip%

    REM Clean up: delete the temp directory
    rmdir /s /q "%temp_dir%"
	
pause
