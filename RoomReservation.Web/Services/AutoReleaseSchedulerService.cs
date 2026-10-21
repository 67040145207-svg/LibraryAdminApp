namespace RoomReservation.Web.Services;

/// <summary>
/// Background Job / Scheduler (IHostedService)
/// ตรวจสอบทุก 20 วินาที:
/// 1. ยกเลิกอัตโนมัติ (Auto-release) ทันทีหากไม่เช็คอินภายใน 15 นาทีหลังถึงเวลาจอง เพื่อคืน Slot ให้ระบบ
/// 2. สร้างรายการแบนระงับสิทธิ์ 2 วันอัตโนมัติ (Penalty System)
/// 3. ส่งแจ้งเตือนก่อนเริ่ม 15 นาที และก่อนหมดเวลา 15 นาที
/// </summary>
public class AutoReleaseSchedulerService : BackgroundService
{
    private readonly ReservationEngineService _engine;
    private readonly ILogger<AutoReleaseSchedulerService> _logger;

    public AutoReleaseSchedulerService(
        ReservationEngineService engine,
        ILogger<AutoReleaseSchedulerService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoReleaseSchedulerService started: monitoring 15-minute No-Show & 2-day Penalty rules.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int affected = _engine.RunAutomatedChecks();
                if (affected > 0)
                {
                    _logger.LogInformation("AutoReleaseScheduler processed {Count} booking state transitions.", affected);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoReleaseSchedulerService.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
