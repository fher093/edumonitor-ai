using Backend.Options;
using Microsoft.Extensions.Options;

namespace Backend.Services;

/// <summary>
/// Ejecuta la sincronización con Moodle cada <c>IntervaloMinutos</c> (RF-03), para que las tareas,
/// notas y matrículas que el docente registra en Moodle aparezcan en EduMonitor sin intervención
/// del administrador. Si ya hay una sincronización en curso (por ejemplo, una manual), omite ese ciclo.
/// Cada ejecución queda en el historial sin usuario asociado ("Automática").
/// </summary>
public sealed class SincronizacionAutomaticaWorker(
    IServiceScopeFactory scopes,
    IOptions<SincronizacionAutomaticaOptions> options,
    TimeProvider clock,
    ILogger<SincronizacionAutomaticaWorker> logger) : BackgroundService
{
    private readonly SincronizacionAutomaticaOptions _opt = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Habilitada)
        {
            logger.LogInformation("Sincronización automática con Moodle deshabilitada.");
            return;
        }

        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _opt.IntervaloMinutos));
        logger.LogInformation("Sincronización automática con Moodle cada {Minutos} min.", intervalo.TotalMinutes);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _opt.RetrasoInicialSegundos)), clock, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo, clock);
            do
            {
                await EjecutarCicloAsync(stoppingToken);
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
    }

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var sincronizacion = scope.ServiceProvider.GetRequiredService<SincronizacionMoodleService>();

            var resultado = await sincronizacion.EjecutarAsync(
                usuarioId: null, usuarioCorreo: null, tokens.CrearTokenSincronizacion().Token, cursos: null, ct);

            if (resultado is null)
                logger.LogInformation("Sincronización automática omitida: ya hay otra en curso.");
            else
                logger.LogInformation("Sincronización automática {Estado}: {Mensaje}", resultado.Estado, resultado.Mensaje);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo inesperado no debe detener el ciclo: se reintenta en el siguiente intervalo.
            logger.LogError(ex, "Error inesperado en la sincronización automática con Moodle.");
        }
    }
}
