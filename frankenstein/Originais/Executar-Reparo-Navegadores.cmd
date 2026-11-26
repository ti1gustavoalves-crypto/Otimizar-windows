@echo off
setlocal
title Reparo de Navegadores
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reparar-Navegadores.ps1"
if errorlevel 1 (
  echo.
  echo O script terminou com erro. Confirme que os dois arquivos estao na mesma pasta.
  pause
)
endlocal
