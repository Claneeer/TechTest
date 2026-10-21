using TechTest.Maui.Services;

namespace TechTest.Maui.Controls;

/// <summary>
/// Barra "✅ Aprovado / ❌ Reprovado" reutilizável. Use <see cref="Attach"/> no construtor de cada página de teste:
/// ela é adicionada na parte inferior sem alterar o XAML da página.
/// </summary>
public class ApprovalBar : ContentView
{
    private readonly TestId _testId;
    private readonly Label _lblStatus;
    private readonly Label _lblMode;
    private readonly Button _btnApprove;
    private readonly Button _btnReject;

    private static readonly Color Green = Color.FromArgb("#22C55E");
    private static readonly Color Red = Color.FromArgb("#EF4444");
    private static readonly Color Muted = Color.FromArgb("#9494B8");
    private static readonly Color Card = Color.FromArgb("#121224");
    private static readonly Color Border = Color.FromArgb("#32325A");

    public ApprovalBar(TestId testId)
    {
        _testId = testId;

        _lblStatus = new Label { FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        _lblMode = new Label { FontSize = 11, TextColor = Muted, VerticalOptions = LayoutOptions.Center };

        _btnApprove = new Button
        {
            Text = "✅ Aprovado",
            BackgroundColor = Color.FromArgb("#143D26"),
            TextColor = Colors.White,
            BorderColor = Green,
            BorderWidth = 1,
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(18, 0)
        };
        _btnReject = new Button
        {
            Text = "❌ Reprovado",
            BackgroundColor = Color.FromArgb("#4A1D1D"),
            TextColor = Colors.White,
            BorderColor = Red,
            BorderWidth = 1,
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(18, 0)
        };
        _btnApprove.Clicked += async (_, _) => await OnResultAsync(TestStatus.Approved);
        _btnReject.Clicked += async (_, _) => await OnResultAsync(TestStatus.Rejected);

        var labels = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _lblStatus, _lblMode } };
        var buttons = new HorizontalStackLayout { Spacing = 10, Children = { _btnReject, _btnApprove } };

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(16, 10)
        };
        grid.Add(labels, 0, 0);
        grid.Add(buttons, 1, 0);

        Content = new Border
        {
            BackgroundColor = Card,
            Stroke = new SolidColorBrush(Border),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = grid
        };

        Refresh();
    }

    /// <summary>Envolve o conteúdo atual da página e acrescenta a barra de aprovação no rodapé.</summary>
    public static ApprovalBar Attach(ContentPage page, TestId testId)
    {
        var original = page.Content;
        var bar = new ApprovalBar(testId) { Margin = new Thickness(20, 0, 20, 14) };

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };

        page.Content = null; // desvincula antes de reparentar
        if (original != null) root.Add(original, 0, 0);
        root.Add(bar, 0, 1);
        page.Content = root;

        page.Appearing += (_, _) => bar.Refresh();
        return bar;
    }

    public void Refresh()
    {
        var session = TestSession.Current;
        var status = session.GetStatus(_testId);

        (_lblStatus.Text, _lblStatus.TextColor) = status switch
        {
            TestStatus.Approved => ("Resultado: ✅ Aprovado", Green),
            TestStatus.Rejected => ("Resultado: ❌ Reprovado", Red),
            _ => ("Resultado: ⏳ Pendente", Muted)
        };

        if (session.SequentialMode)
        {
            int pos = Array.IndexOf(TestSession.Order, _testId) + 1;
            _lblMode.Text = $"Modo sequencial — teste {pos}/{TestSession.Order.Length} ({TestSession.TitleOf(_testId)}). Aprovar avança para o próximo.";
        }
        else
        {
            _lblMode.Text = $"Registre o resultado do teste de {TestSession.TitleOf(_testId)}.";
        }
    }

    private async Task OnResultAsync(TestStatus status)
    {
        var session = TestSession.Current;
        session.SetStatus(_testId, status);
        Refresh();

        if (status != TestStatus.Approved || !session.SequentialMode) return;

        var next = session.NextAfter(_testId);
        if (next.HasValue)
        {
            await Shell.Current.GoToAsync($"//{TestSession.RouteOf(next.Value)}");
        }
        else
        {
            session.StopSequential();
            await Shell.Current.GoToAsync("//MainPage");
            var page = Shell.Current.CurrentPage;
            if (page != null)
                await page.DisplayAlert("Modo Sequencial", "Todos os testes foram concluídos. Gere o Laudo Técnico na tela inicial.", "OK");
        }
    }
}
