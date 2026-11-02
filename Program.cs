using ContingenciaWms;
using ContingenciaWms.Configuration;
using ContingenciaWms.Repositories;
using ContingenciaWms.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configuración de directorio de logs seguro
var logDir = builder.Configuration.GetValue<string>("LogDirectory") ?? @"D:\LogsContingenciaWMS\";
try
{
    if (!Directory.Exists(logDir))
    {
        Directory.CreateDirectory(logDir);
    }
}
catch
{
    logDir = AppContext.BaseDirectory;
}

var logFilePath = Path.Combine(logDir, "log-.txt");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<IContingenciaWmsRepo, ContingenciaWmsRepo>();
builder.Services.AddScoped<IEmailSenderContingencia, EmailSenderContingencia>();

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CBF_Contingencia_WMS";
});

var host = builder.Build();
host.Run();
