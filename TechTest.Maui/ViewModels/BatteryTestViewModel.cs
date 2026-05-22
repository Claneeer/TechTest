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

    public BatteryTestViewModel(IBatteryService batteryService)
    {
        _batteryService = batteryService;

        RefreshCommand = new Command(RefreshStatus);
        GenerateReportCommand = new Command(async () => await GenerateReportAsync());

        RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (_batteryService == null) return;

        try
        {
            var status = _batteryService.GetCurrentStatus();
            if (status != null)
            {
                ChargePercent = status.ChargePercent;
                IsCharging = status.IsCharging;
                PowerSource = status.PowerSource;
                TimeRemaining = status.TimeRemaining;
                StatusText = status.StatusText;
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
