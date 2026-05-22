using TechTest.Maui.Services;

namespace TechTest.Maui.ViewModels;

public class SpecsViewModel : BaseViewModel
{
    private readonly IHardwareInfoService _hardwareInfoService;

    public SpecsViewModel(IHardwareInfoService hardwareInfoService)
    {
        _hardwareInfoService = hardwareInfoService;
    }
}
