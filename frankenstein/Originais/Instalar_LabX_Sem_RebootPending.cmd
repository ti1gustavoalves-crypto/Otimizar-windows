@echo off
setlocal EnableExtensions
title Instalar LabX sem RebootPending

:: Elevar para administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cls
echo ============================================================
echo   LABX - LIMPAR REBOOTPENDING E INSTALAR SEM REINICIAR
echo ============================================================
echo.
echo O diagnostico mostrou que o Windows recria:
echo PendingFileRenameOperations
echo apontando para gamingservicesproxy_13.dll.0
echo.
echo Por isso, reiniciar antes de abrir o LabX faz o problema voltar.
echo.
echo Este arquivo vai:
echo  1. Parar GamingServices e GamingServicesNet
echo  2. Remover PendingFileRenameOperations
echo  3. Verificar se ele sumiu
echo  4. Abrir D:\LabX\LabXBundle.exe imediatamente, SEM reiniciar
echo.
pause

set "BACKUP=%USERPROFILE%\Desktop\SessionManager_Antes_LabX.reg"

echo.
echo [1/5] Criando backup...
reg export "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager" "%BACKUP%" /y >nul 2>&1

echo [2/5] Parando Gaming Services...
sc stop GamingServices >nul 2>&1
sc stop GamingServicesNet >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3/5] Removendo PendingFileRenameOperations...
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager" /v PendingFileRenameOperations /f >nul 2>&1

echo [4/5] Verificando...
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager" /v PendingFileRenameOperations >nul 2>&1
if %errorlevel%==0 (
    echo.
    echo ERRO: o marcador ainda existe.
    echo Nao vou abrir o instalador para evitar novo bloqueio.
    echo.
    reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager" /v PendingFileRenameOperations
    echo.
    pause
    exit /b 1
)

echo Marcador removido com sucesso.

echo [5/5] Abrindo LabX...
if not exist "D:\LabX\LabXBundle.exe" (
    echo.
    echo ERRO: nao encontrei:
    echo D:\LabX\LabXBundle.exe
    echo.
    echo Abra manualmente o LabXBundle.exe AGORA, sem reiniciar o Windows.
    echo.
    pause
    exit /b 2
)

echo.
echo IMPORTANTE: NAO REINICIE o computador agora.
echo O instalador sera aberto imediatamente.
echo.
start "" /wait "D:\LabX\LabXBundle.exe"

echo.
echo Instalador do LabX foi fechado.
echo.
echo Reativando Gaming Services...
sc start GamingServices >nul 2>&1
sc start GamingServicesNet >nul 2>&1

echo.
echo Concluido.
echo Backup da chave:
echo %BACKUP%
echo.
pause
endlocal
