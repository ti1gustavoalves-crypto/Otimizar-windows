@echo off
title Configurar Licenca Siemens NX - FIEMG
color 1F

:: Verifica se esta executando como administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo Solicitando permissao de administrador...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================================
echo        CONFIGURACAO DO SERVIDOR DE LICENCA SIEMENS NX
echo ============================================================
echo.
echo Servidor que sera configurado:
echo 28000@minduri.fiemg.com.br
echo.

:: Fecha o NX caso esteja aberto, pois processos ja iniciados
:: mantem as variaveis antigas em memoria.
taskkill /F /IM ugraf.exe >nul 2>&1
taskkill /F /IM nx.exe >nul 2>&1

:: Configura para o usuario atual
reg add "HKCU\Environment" /v SPLM_LICENSE_SERVER /t REG_SZ /d "28000@minduri.fiemg.com.br" /f >nul
reg add "HKCU\Environment" /v UGS_LICENSE_SERVER /t REG_SZ /d "28000@minduri.fiemg.com.br" /f >nul

:: Configura para todos os usuarios do computador
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SPLM_LICENSE_SERVER /t REG_SZ /d "28000@minduri.fiemg.com.br" /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v UGS_LICENSE_SERVER /t REG_SZ /d "28000@minduri.fiemg.com.br" /f >nul

:: Atualiza tambem via SETX
setx SPLM_LICENSE_SERVER "28000@minduri.fiemg.com.br" >nul
setx UGS_LICENSE_SERVER "28000@minduri.fiemg.com.br" >nul
setx /M SPLM_LICENSE_SERVER "28000@minduri.fiemg.com.br" >nul
setx /M UGS_LICENSE_SERVER "28000@minduri.fiemg.com.br" >nul

echo.
echo ============================================================
echo CONFIGURACAO CONCLUIDA
echo ============================================================
echo.
echo SPLM_LICENSE_SERVER = 28000@minduri.fiemg.com.br
echo UGS_LICENSE_SERVER  = 28000@minduri.fiemg.com.br
echo.
echo Feche e abra novamente o Siemens NX.
echo Caso o NX ainda mostre localhost, reinicie o computador.
echo.
pause
