using ContingenciaWms.DTOs;
using ContingenciaWms.Repositories;
using ContingenciaWms.Services;

namespace ContingenciaWms
{
    public class Worker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<Worker> _logger;

        private bool _enEjecucion = false;

        public Worker(ILogger<Worker> logger, IConfiguration config, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int versionLocal = -1;
            int intervalo = _config.GetValue<int>("IntervaloSegundos");
            if (intervalo < 5) intervalo = 60;

            int diasAtras = _config.GetValue<int>("DiasAtras");
            if (diasAtras < 0) diasAtras = 3;

            _logger.LogInformation("Servicio Windows de Contingencia WMS / SAP iniciado a las {Fecha:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
            _logger.LogInformation("Parámetros base: Intervalo = {intervalo}s | Rango = últimos {dias} días.", intervalo, diasAtras);

            var ultimaEjecucion = DateTime.MinValue;
            ConfiguracionRutinaDTO? configRutina = null;
            bool estadoAnteriorHabilitado = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IContingenciaWmsRepo>();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSenderContingencia>();

                    // 1. Verificación en tiempo real de cambios en Intranet (Hot-Reload cada 3 segundos)
                    int versionRemota = await repo.ObtenerVersionConfigAsync();
                    if (versionRemota != versionLocal || configRutina == null)
                    {
                        configRutina = await repo.ObtenerConfiguracionRutinaAsync(intervalo, diasAtras);
                        versionLocal = versionRemota;

                        intervalo = configRutina.IntervaloSegundos;
                        diasAtras = configRutina.DiasAtras;

                        _logger.LogInformation(">>> [HOT-RELOAD] Configuración sincronizada desde Intranet (Versión: {ver}). Habilitado: {hab}, Intervalo: {inter}s, Días atrás: {dias}, Correo: {correo}",
                            versionLocal, configRutina.Habilitado, intervalo, diasAtras, configRutina.CanalCorreo);

                        estadoAnteriorHabilitado = configRutina.Habilitado;
                    }

                    // 2. Si la rutina fue desactivada desde la Intranet, pausar temporalmente
                    if (!configRutina.Habilitado)
                    {
                        if (estadoAnteriorHabilitado)
                        {
                            _logger.LogWarning("La rutina CONTINGENCIA_WMS fue DESACTIVADA desde la Intranet. En espera de reactivación...");
                            estadoAnteriorHabilitado = false;
                        }

                        await Task.Delay(3000, stoppingToken);
                        continue;
                    }

                    estadoAnteriorHabilitado = true;

                    // 3. Evaluar si ya transcurrió el intervalo en segundos
                    double segundosTranscurridos = (DateTime.Now - ultimaEjecucion).TotalSeconds;
                    if (segundosTranscurridos >= intervalo && !_enEjecucion)
                    {
                        _enEjecucion = true;
                        var inicioProceso = DateTime.Now;

                        try
                        {
                            _logger.LogInformation("Escaneando discrepancias PVD vs SAP HANA [Rango: {dias} días]...", diasAtras);

                            var resultados = await repo.EjecutarContingenciaAsync(diasAtras);
                            var rescatadas = resultados.Where(r => r.DocEntry > 0).ToList();

                            if (rescatadas.Count == 0)
                            {
                                _logger.LogInformation("Sin órdenes desfasadas detectadas. Todo sincronizado correctamente.");
                            }
                            else
                            {
                                _logger.LogInformation(">>> Órdenes rescatadas: {count}", rescatadas.Count);
                                foreach (var item in rescatadas)
                                {
                                    _logger.LogInformation(
                                        "DOCENTRY PVD: {pvd} | SAP DOCNUM: {sap} | {ant} -> {nuevo} | SAP FLAG: {flag}",
                                        item.DocEntry, item.DocNumSap, item.EstadoAnterior, item.EstadoNuevo, item.EstadoSap
                                    );
                                }

                                // 4. Envío de correos si el canal está habilitado para esta rutina
                                if (configRutina.CanalCorreo)
                                {
                                    var suscriptores = await repo.ObtenerSuscriptoresCorreoAsync("CONTINGENCIA_WMS");
                                    if (suscriptores.Count > 0)
                                    {
                                        _logger.LogInformation("Despachando correo de contingencia a {Count} suscriptor(es)...", suscriptores.Count);
                                        await emailSender.EnviarAlertaRescateAsync(rescatadas, suscriptores);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Canal Correo activado en Intranet, pero no hay suscriptores de correo registrados para CONTINGENCIA_WMS.");
                                    }
                                }
                            }

                            ultimaEjecucion = DateTime.Now;
                            var duracion = (DateTime.Now - inicioProceso).TotalSeconds;
                            _logger.LogInformation("Ciclo finalizado en {segundos:F2}s. Siguiente escaneo en {intervalo}s.", duracion, intervalo);
                        }
                        finally
                        {
                            _enEjecucion = false;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la ejecución del ciclo de contingencia WMS");
                }

                // Heartbeat estándar y uniforme (pausa breve de 3 segundos)
                try
                {
                    await Task.Delay(3000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Servicio Windows de Contingencia WMS finalizado.");
        }
    }
}
