@echo off
REM Double-click this file to install the ArcGIS Pro MCP Bridge.
REM It runs the PowerShell installer with execution policy bypassed for this run only.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
echo.
pause
