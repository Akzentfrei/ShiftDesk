@echo off
rem Baut ShiftDesk.exe mit dem C#-Compiler, der bei Windows dabei ist (.NET Framework 4).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /out:ShiftDesk.exe ^
  /win32icon:assets\ShiftDesk.ico /resource:assets\logo.png,ShiftDesk.logo.png ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:System.IO.Compression.dll ^
  src\*.cs
