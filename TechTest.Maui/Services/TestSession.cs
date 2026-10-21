namespace TechTest.Maui.Services;

public enum TestId
{
    Keyboard,
    Touchpad,
    DeadPixel,
    Camera,
    Microphone,
    Audio,
    Battery
}

public enum TestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Estado global da sessão de QA: resultado (Aprovado/Reprovado) de cada teste e controle do Modo Sequencial.
/// </summary>
public sealed class TestSession
{
    public static TestSession Current { get; } = new();

    /// <summary>Ordem usada pelo Modo Sequencial (Testar Tudo).</summary>
    public static readonly TestId[] Order =
    {
        TestId.Keyboard, TestId.Touchpad, TestId.DeadPixel, TestId.Camera,
        TestId.Microphone, TestId.Audio, TestId.Battery
    };

    private readonly Dictionary<TestId, TestStatus> _results = new();

    public bool SequentialMode { get; private set; }

    public event Action? Changed;

    public static string RouteOf(TestId id) => id switch
    {
        TestId.Keyboard => "KeyboardTestPage",
        TestId.Touchpad => "TouchpadTestPage",
        TestId.DeadPixel => "DeadPixelTestPage",
        TestId.Camera => "CameraTestPage",
        TestId.Microphone => "MicrophoneTestPage",
        TestId.Audio => "AudioTestPage",
        TestId.Battery => "BatteryTestPage",
        _ => "MainPage"
    };

    public static string TitleOf(TestId id) => id switch
    {
        TestId.Keyboard => "Teclado",
        TestId.Touchpad => "Touchpad",
        TestId.DeadPixel => "Pixel Morto",
        TestId.Camera => "Webcam",
        TestId.Microphone => "Microfone",
        TestId.Audio => "Alto-falantes",
        TestId.Battery => "Bateria",
        _ => id.ToString()
    };

    public TestStatus GetStatus(TestId id) =>
        _results.TryGetValue(id, out var s) ? s : TestStatus.Pending;

    public void SetStatus(TestId id, TestStatus status)
    {
        _results[id] = status;
        Changed?.Invoke();
    }

    public int Count(TestStatus status) => Order.Count(id => GetStatus(id) == status);

    public int Total => Order.Length;

    public void Reset()
    {
        _results.Clear();
        SequentialMode = false;
        Changed?.Invoke();
    }

    public void StartSequential()
    {
        SequentialMode = true;
        Changed?.Invoke();
    }

    public void StopSequential()
    {
        SequentialMode = false;
        Changed?.Invoke();
    }

    /// <summary>Primeiro teste que ainda não foi aprovado (usado ao iniciar o modo sequencial).</summary>
    public TestId? FirstNotApproved()
    {
        foreach (var id in Order)
            if (GetStatus(id) != TestStatus.Approved) return id;
        return null;
    }

    /// <summary>Próximo teste após o atual que ainda não foi aprovado; null quando acabou.</summary>
    public TestId? NextAfter(TestId current)
    {
        int idx = Array.IndexOf(Order, current);
        for (int i = idx + 1; i < Order.Length; i++)
            if (GetStatus(Order[i]) != TestStatus.Approved) return Order[i];
        return null;
    }
}
