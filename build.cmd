@echo off
rem Baut MausHighlighter.exe mit dem in Windows enthaltenen .NET-Framework-Compiler.
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:winexe /platform:anycpu ^
  /out:MausHighlighter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll MausHighlighter.cs
