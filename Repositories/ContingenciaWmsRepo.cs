using ContingenciaWms.DTOs;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ContingenciaWms.Repositories
{
    public class ContingenciaWmsRepo : IContingenciaWmsRepo
    {
        private readonly string _connectionString;
        private readonly string _bdHana;
        private readonly ILogger<ContingenciaWmsRepo> _logger;

        public ContingenciaWmsRepo(IConfiguration configuration, ILogger<ContingenciaWmsRepo> logger)
        {
            _logger = logger;

            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada en appsettings.json.");

            _bdHana = configuration.GetConnectionString("BdHana") 
                ?? configuration["BdHana"] 
                ?? throw new InvalidOperationException("El parámetro de base de datos 'BdHana' no está configurado en appsettings.json.");
        }

        public async Task<int> ObtenerVersionConfigAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_VERSION_CONFIG_ALERTAS", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Convert.ToInt32(reader["VERSION_CONFIG"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo verificar la versión de configuración en SQL Server: {msg}", ex.Message);
            }

            return 1;
        }

        public async Task<ConfiguracionRutinaDTO> ObtenerConfiguracionRutinaAsync(int defIntervalo, int defDiasAtras)
        {
            var config = new ConfiguracionRutinaDTO
            {
                IntervaloSegundos = defIntervalo >= 5 ? defIntervalo : 60,
                DiasAtras = defDiasAtras >= 0 ? defDiasAtras : 3,
                Habilitado = true,
                CanalCorreo = false,
                CanalIntranet = true
            };

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_CONFIG_RUTINA", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@CODIGO_RUTINA", SqlDbType.VarChar, 50) { Value = "CONTINGENCIA_WMS" });

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    config.Habilitado = Convert.ToBoolean(reader["HABILITADO"]);
                    int intervalo = Convert.ToInt32(reader["INTERVALO_SEGUNDOS"]);
                    config.IntervaloSegundos = intervalo >= 5 ? intervalo : 5;
                    config.DiasAtras = Convert.ToInt32(reader["DIAS_ATRAS"]);
                    config.CanalCorreo = Convert.ToBoolean(reader["CANAL_CORREO"]);
                    config.CanalIntranet = Convert.ToBoolean(reader["CANAL_INTRANET"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo leer la configuración de la rutina desde la base de datos (usando valores locales): {msg}", ex.Message);
            }

            return config;
        }

        public async Task<int> ObtenerIntervaloSegundosAsync(int valorPorDefecto)
        {
            var config = await ObtenerConfiguracionRutinaAsync(valorPorDefecto, 3);
            return config.Habilitado ? config.IntervaloSegundos : -1;
        }

        public async Task<List<SuscriptorCorreoDTO>> ObtenerSuscriptoresCorreoAsync(string codigoRutina = "CONTINGENCIA_WMS")
        {
            var lista = new List<SuscriptorCorreoDTO>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("dbo.SP_OBTENER_SUSCRIPTORES_RUTINA_CORREO", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add(new SqlParameter("@CODIGO_RUTINA", SqlDbType.VarChar, 50) { Value = codigoRutina });

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string correo = reader["CORREO"]?.ToString()?.Trim() ?? string.Empty;
                    string nombre = reader["NOMBRE_COMPLETO"]?.ToString()?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(correo))
                    {
                        lista.Add(new SuscriptorCorreoDTO
                        {
                            Correo = correo,
                            NombreCompleto = nombre
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo obtener la lista de suscriptores de correo: {msg}", ex.Message);
            }

            return lista;
        }

        public async Task<List<ResultadoContingenciaDTO>> EjecutarContingenciaAsync(int diasAtras)
        {
            var resultados = new List<ResultadoContingenciaDTO>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                using var command = new SqlCommand("dbo.SP_CONTINGENCIA_WMS_REINTENTO", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 120 // 2 minutos máximo
                };

                command.Parameters.Add(new SqlParameter("@BD_HANA", SqlDbType.VarChar, 50) { Value = _bdHana });
                command.Parameters.Add(new SqlParameter("@DIAS_ATRAS", SqlDbType.Int) { Value = diasAtras });

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    resultados.Add(new ResultadoContingenciaDTO
                    {
                        DocEntry = Convert.ToInt32(reader["DOCENTRY"]),
                        DocNumSap = Convert.ToInt32(reader["DOCNUM_SAP"]),
                        DocEntrySap = Convert.ToInt32(reader["DOCENTRY_SAP"]),
                        EstadoAnterior = reader["ESTADO_ANTERIOR"] != DBNull.Value ? reader["ESTADO_ANTERIOR"].ToString() : null,
                        EstadoNuevo = reader["ESTADO_NUEVO"] != DBNull.Value ? reader["ESTADO_NUEVO"].ToString() : null,
                        EstadoSap = reader["ESTADO_SAP"] != DBNull.Value ? reader["ESTADO_SAP"].ToString() : null,
                        Mensaje = reader["MENSAJE"] != DBNull.Value ? reader["MENSAJE"].ToString() : null,
                        FechaCambio = Convert.ToDateTime(reader["FECHA_CAMBIO"])
                    });
                }

                if (resultados.Any(r => r.EstadoSap == "ERROR"))
                {
                    var error = resultados.First(r => r.EstadoSap == "ERROR");
                    _logger.LogError("El SP devolvió un error controlado: {mensaje}", error.Mensaje);
                }
                else if (resultados.Count > 0)
                {
                    _logger.LogInformation(">>> ¡ÉXITO! Se rescataron {count} órdenes desfasadas para reintento en WMS.", resultados.Count);
                }

                return resultados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error técnico al ejecutar el procedimiento de contingencia WMS");
                throw;
            }
        }
    }
}
