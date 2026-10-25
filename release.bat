@echo off
rem Baut ShiftDesk.exe und packt sie mit der Anleitung in ShiftDesk-<Version>.zip zum Weitergeben.
cd /d "%~dp0"
call "%~dp0build.bat"
if errorlevel 1 exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$v = (Get-Item 'ShiftDesk.exe').VersionInfo.FileVersion -replace '\.0$', '';" ^
  "$zip = \"ShiftDesk-$v.zip\";" ^
  "Compress-Archive -Path 'ShiftDesk.exe', 'Anleitung.txt' -DestinationPath $zip -Force;" ^
  "Write-Host ('Fertig: ' + $zip + ' (' + [math]::Round((Get-Item $zip).Length / 1KB) + ' KB)')"
