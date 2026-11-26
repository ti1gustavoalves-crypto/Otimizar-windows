@echo off
setlocal EnableExtensions
title Configurar Licenca NX - FIEMG - Windows 7
color 1F

set "SERVIDOR=28000@minduri.fiemg.com.br"

:: ============================================================
:: Verifica privilegios de administrador
:: ============================================================
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo.
    echo Este programa precisa ser executado como Administrador.
    echo Solicitando permissao...
    echo.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cls
echo ============================================================
echo       CONFIGURADOR DE LICENCA NX - FIEMG - WINDOWS 7
echo ============================================================
echo.
echo Servidor de licenca:
echo %SERVIDOR%
echo.
echo O programa configurara:
echo   SPLM_LICENSE_SERVER
echo   UGS_LICENSE_SERVER
echo.
echo para o usuario atual e para todo o computador.
echo.
echo ------------------------------------------------------------
echo.

:: ============================================================
:: Fecha processos conhecidos do NX, se estiverem abertos
:: ============================================================
tasklist /FI "IMAGENAME eq ugraf.exe" 2>nul | find /I "ugraf.exe" >nul
if "%errorlevel%"=="0" (
    echo Fechando Siemens NX...
    taskkill /F /IM ugraf.exe >nul 2>&1
)

tasklist /FI "IMAGENAME eq nx.exe" 2>nul | find /I "nx.exe" >nul
if "%errorlevel%"=="0" (
    echo Fechando Siemens NX...
    taskkill /F /IM nx.exe >nul 2>&1
)

:: ============================================================
:: Grava no perfil do usuario
:: ============================================================
echo Configurando usuario atual...
reg add "HKCU\Environment" /v SPLM_LICENSE_SERVER /t REG_SZ /d "%SERVIDOR%" /f >nul 2>&1
reg add "HKCU\Environment" /v UGS_LICENSE_SERVER /t REG_SZ /d "%SERVIDOR%" /f >nul 2>&1

:: ============================================================
:: Grava nas variaveis do sistema
:: ============================================================
echo Configurando todo o computador...
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SPLM_LICENSE_SERVER /t REG_SZ /d "%SERVIDOR%" /f >nul 2>&1
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v UGS_LICENSE_SERVER /t REG_SZ /d "%SERVIDOR%" /f >nul 2>&1

:: Tambem deixa os valores disponiveis nesta sessao do BAT
set "SPLM_LICENSE_SERVER=%SERVIDOR%"
set "UGS_LICENSE_SERVER=%SERVIDOR%"

:: ============================================================
:: Verificacao
:: ============================================================
echo.
echo ============================================================
echo                    VERIFICACAO
echo ============================================================
echo.

set "ERRO=0"

reg query "HKCU\Environment" /v SPLM_LICENSE_SERVER 2>nul | find /I "%SERVIDOR%" >nul
if "%errorlevel%"=="0" (
    echo [OK] Usuario - SPLM_LICENSE_SERVER
) else (
    echo [ERRO] Usuario - SPLM_LICENSE_SERVER
    set "ERRO=1"
)

reg query "HKCU\Environment" /v UGS_LICENSE_SERVER 2>nul | find /I "%SERVIDOR%" >nul
if "%errorlevel%"=="0" (
    echo [OK] Usuario - UGS_LICENSE_SERVER
) else (
    echo [ERRO] Usuario - UGS_LICENSE_SERVER
    set "ERRO=1"
)

reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v SPLM_LICENSE_SERVER 2>nul | find /I "%SERVIDOR%" >nul
if "%errorlevel%"=="0" (
    echo [OK] Sistema - SPLM_LICENSE_SERVER
) else (
    echo [ERRO] Sistema - SPLM_LICENSE_SERVER
    set "ERRO=1"
)

reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v UGS_LICENSE_SERVER 2>nul | find /I "%SERVIDOR%" >nul
if "%errorlevel%"=="0" (
    echo [OK] Sistema - UGS_LICENSE_SERVER
) else (
    echo [ERRO] Sistema - UGS_LICENSE_SERVER
    set "ERRO=1"
)

echo.
if "%ERRO%"=="0" (
    color 2F
    echo ============================================================
    echo               CONFIGURACAO CONCLUIDA COM SUCESSO
    echo ============================================================
    echo.
    echo O servidor foi gravado como:
    echo %SERVIDOR%
    echo.
    echo RECOMENDADO:
    echo Reinicie o computador antes de abrir novamente o Siemens NX.
) else (
    color 4F
    echo ============================================================
    echo              HOUVE ERRO NA CONFIGURACAO
    echo ============================================================
    echo.
    echo Verifique se o arquivo foi executado como Administrador.
)

echo.
echo Pressione qualquer tecla para fechar.
pause >nul
endlocal
