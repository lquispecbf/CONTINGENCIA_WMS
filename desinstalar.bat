@echo off
setlocal
echo ===============================================
echo  DESINSTALANDO SERVICIO CBF CONTINGENCIA WMS
echo ===============================================
set SERVICE_NAME=CBFContingenciaWMS

sc stop "%SERVICE_NAME%"
sc delete "%SERVICE_NAME%"

echo.
echo ✅ Servicio desinstalado correctamente.
pause
