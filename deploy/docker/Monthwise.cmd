@echo off
rem Double-click (or pin to the taskbar) to start Monthwise and open it in the browser.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0monthwise.ps1" start -Open
if errorlevel 1 pause
