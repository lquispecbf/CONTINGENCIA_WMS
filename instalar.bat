@echo off
setlocal

echo ===============================================
echo  INSTALANDO SERVICIO CBF CONTINGENCIA WMS
echo ===============================================

set SERVICE_NAME=CBFContingenciaWMS
set SERVICE_DISPLAY_NAME=CBFContingenciaWMS
set SERVICE_DESCRIPTION=Servicio de Contingencia WMS
set SERVICE_EXE=%~dp0ContingenciaWms.exe

rem Verificar que el archivo exista
if not exist "%SERVICE_EXE%" (
    echo ERROR: No se encontró el archivo "%SERVICE_EXE%"
    pause
    exit /b 1
)

rem Detener y eliminar si ya existe
sc stop "%SERVICE_NAME%" >nul 2>&1
sc delete "%SERVICE_NAME%" >nul 2>&1

rem Crear el servicio
sc create "%SERVICE_NAME%" binPath= "%SERVICE_EXE%" start= auto
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"
sc start "%SERVICE_NAME%"

echo.
echo ✅ Servicio instalado correctamente.
pause
