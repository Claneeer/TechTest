using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class BatteryTestViewModel : BaseViewModel
{
    private readonly IBatteryService _batteryService;

    private int _chargePercent;
    private bool _isCharging;
    private string _powerSource = "Desconhecido";
    private string _timeRemaining = "Calculando...";
    private string _statusText = "Calculando...";

    private string _designCapacity = "Carregando...";
    private string _fullChargeCapacity = "Carregando...";
    private string _health = "Carregando...";
    private string _cycleCount = "Carregando...";
    private string _manufacturer = "Carregando...";
    private string _reportFilePath = "";
    private bool _isGeneratingReport;
    private bool _isReportReady;

    public int ChargePercent
    {
        get => _chargePercent;
        set => SetProperty(ref _chargePercent, value);
    }

    public bool IsCharging
    {
        get => _isCharging;
        set => SetProperty(ref _isCharging, value);
    }

    public string PowerSource
    {
        get => _powerSource;
        set => SetProperty(ref _powerSource, value);
    }

    public string TimeRemaining
    {
        get => _timeRemaining;
        set => SetProperty(ref _timeRemaining, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string DesignCapacity
    {
        get => _designCapacity;
        set => SetProperty(ref _designCapacity, value);
    }

    public string FullChargeCapacity
    {
        get => _fullChargeCapacity;
        set => SetProperty(ref _fullChargeCapacity, value);
    }

    public string Health
    {
        get => _health;
        set => SetProperty(ref _health, value);
    }

    public string CycleCount
    {
        get => _cycleCount;
        set => SetProperty(ref _cycleCount, value);
    }

    public string Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
    }

    public string ReportFilePath
    {
        get => _reportFilePath;
        set => SetProperty(ref _reportFilePath, value);
    }

    public bool IsGeneratingReport
    {
        get => _isGeneratingReport;
        set => SetProperty(ref _isGeneratingReport, value);
    }

    public bool IsReportReady
    {
        get => _isReportReady;
        set => SetProperty(ref _isReportReady, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand GenerateReportCommand { get; }

    private bool _hasBattery = true;
    private string _powerType = "";
    private bool _initialized;

    /// <summary>False em desktops: a UI pode ocultar gauge/botões e exibir <see cref="StatusText"/>.</summary>
    public bool HasBattery
    {
        get => _hasBattery;
        set => SetProperty(ref _hasBattery, value);
    }

    /// <summary>"AC" ou "DC".</summary>
    public string PowerType
    {
        get => _powerType;
        set => SetProperty(ref _powerType, value);
    }

    public BatteryTestViewModel(IBatteryService batteryService)
    {
        _batteryService = batteryService;

        RefreshCommand = new Command(async () => await RefreshStatusAsync());
        GenerateReportCommand = new Command(async () => await GenerateReportAsync());

        // Dispara imediatamente ao criar a página, sem esperar clique do usuário.
        _ = InitializeAsync();
    }

    /// <summary>Status rápido + detalhes avançados (powercfg) em segundo plano. Idempotente.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            await RefreshStatusAsync();
            if (HasBattery)
                await GenerateReportAsync();
        }
        catch
        {
            _initialized = false; // permite nova tentativa no próximo OnAppearing
        }
    }

    public void RefreshStatus() => _ = RefreshStatusAsync();

    public async Task RefreshStatusAsync()
    {
        if (_batteryService == null) return;

        try
        {
            // WMI é síncrono: roda fora da thread de UI
            var status = await Task.Run(() => _batteryService.GetCurrentStatus());
            if (status == null) return;

            HasBattery = status.HasBattery;
            ChargePercent = status.ChargePercent;
            IsCharging = status.IsCharging;
            PowerType = status.PowerType;
            PowerSource = status.PowerSource;
            TimeRemaining = status.TimeRemaining;
            StatusText = status.StatusText;

            if (!status.HasBattery)
            {
                DesignCapacity = FullChargeCapacity = Health = CycleCount = Manufacturer = BatteryStatus.NoBatteryMessage;
                IsReportReady = false;
            }
        }
        catch
        {
            StatusText = "Erro ao obter status";
        }
    }

    public async Task GenerateReportAsync()
    {
        if (_batteryService == null || IsGeneratingReport) return;

        try
        {
            IsGeneratingReport = true;
            IsReportReady = false;

            var report = await _batteryService.GenerateReportAsync();
            if (report != null)
            {
                DesignCapacity = report.DesignCapacity;
                FullChargeCapacity = report.FullChargeCapacity;
                Health = report.Health;
                CycleCount = report.CycleCount;
                Manufacturer = report.Manufacturer;
                ReportFilePath = report.ReportFilePath;
                IsReportReady = report.IsReportReady;
            }
        }
        catch
        {
            IsReportReady = false;
        }
        finally
        {
            IsGeneratingReport = false;
        }
    }
}
