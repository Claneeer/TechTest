namespace TechTest.Maui.Services;

public class BatteryStatus
{
    public const string NoBatteryMessage = "Bateria não detectada (Computador de Mesa/Sem Bateria)";

    /// <summary>False em desktops/equipamentos sem bateria instalada.</summary>
    public bool HasBattery { get; set; } = true;
    public int ChargePercent { get; set; }
    public bool IsCharging { get; set; }
    /// <summary>"AC" (tomada) ou "DC" (bateria); vazio quando não aplicável.</summary>
    public string PowerType { get; set; } = "";
    public string PowerSource { get; set; } = "";
    public string TimeRemaining { get; set; } = "";
    public string StatusText { get; set; } = "";
}

public class BatteryReport
{
    public string DesignCapacity { get; set; } = "";
    public string FullChargeCapacity { get; set; } = "";
    public string Health { get; set; } = "";
    public string CycleCount { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ReportFilePath { get; set; } = "";
    public bool IsReportReady { get; set; }
}

public interface IBatteryService
{
    BatteryStatus GetCurrentStatus();
    Task<BatteryReport> GenerateReportAsync();
}
