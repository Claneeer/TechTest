using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TechTest.Maui.Services;

public class ReportResult
{
    public string HtmlPath { get; set; } = "";
    public string JsonPath { get; set; } = "";
    public string Verdict { get; set; } = "";
}

/// <summary>Gera o Laudo Técnico (HTML pronto para impressão + JSON) com specs, saúde de bateria/SSD e resultados dos testes.</summary>
public class ReportService
{
    private readonly IHardwareInfoService? _hardware;
    private readonly IBatteryService? _battery;

    public ReportService()
    {
        var services = IPlatformApplication.Current?.Services;
        _hardware = services?.GetService<IHardwareInfoService>();
        _battery = services?.GetService<IBatteryService>();
    }

    public static string ReportsFolder
    {
        get
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(docs)) docs = Path.GetTempPath();
            return Path.Combine(docs, "TechTest", "Laudos");
        }
    }

    public async Task<ReportResult> GenerateAsync(string technician, string notes)
    {
        var specs = _hardware != null ? await _hardware.GetHardwareSpecsAsync() : new HardwareSpecs
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString()
        };

        BatteryReport? battery = null;
        if (_battery != null)
        {
            try
            {
                var task = _battery.GenerateReportAsync();
                // powercfg /batteryreport pode demorar; não trava o laudo
                if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(45))) == task)
                    battery = await task;
            }
            catch
            {
                // Sem bateria (desktop) ou sem permissão
            }
        }

        var session = TestSession.Current;
        var now = DateTime.Now;
        string verdict = ComputeVerdict(session);

        var tests = TestSession.Order.Select(id => new
        {
            id = id.ToString(),
            name = TestSession.TitleOf(id),
            status = StatusText(session.GetStatus(id))
        }).ToList();

        var data = new
        {
            generatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
            technician,
            notes,
            verdict,
            machine = new
            {
                specs.Manufacturer,
                specs.Model,
                specs.SystemFamily,
                specs.SystemSku,
                serialNumber = specs.ServiceTag,
                baseBoard = $"{specs.BaseBoardManufacturer} {specs.BaseBoardProduct}".Trim(),
                specs.MachineName,
                specs.OSVersion
            },
            hardware = new
            {
                cpu = specs.CPU,
                ram = specs.RAM,
                gpus = specs.GPUs.Select(g => g.Name).ToList(),
                resolution = specs.ScreenResolution,
                refreshRateHz = specs.ScreenRefreshRate
            },
            storage = specs.StorageDevices.Select(s => new
            {
                s.Name, s.Type, total = s.FormattedTotalSize, free = s.FormattedFreeSpace, format = s.DriveFormat
            }).ToList(),
            diskHealth = specs.DiskHealth.Select(d => new
            {
                d.Name, d.MediaType, d.HealthStatus,
                healthPercent = d.HealthPercent,
                temperatureC = d.TemperatureC,
                powerOnHours = d.PowerOnHours
            }).ToList(),
            battery = battery == null ? null : new
            {
                battery.DesignCapacity, battery.FullChargeCapacity, battery.Health, battery.CycleCount, battery.Manufacturer
            },
            tests
        };

        Directory.CreateDirectory(ReportsFolder);
        string serialPart = Sanitize(string.IsNullOrWhiteSpace(specs.ServiceTag) ? specs.MachineName : specs.ServiceTag);
        string baseName = $"Laudo_{serialPart}_{now:yyyyMMdd_HHmmss}";
        string htmlPath = Path.Combine(ReportsFolder, baseName + ".html");
        string jsonPath = Path.Combine(ReportsFolder, baseName + ".json");

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(data, jsonOptions), new UTF8Encoding(true));
        await File.WriteAllTextAsync(htmlPath, BuildHtml(technician, notes, now, verdict, specs, battery, session), new UTF8Encoding(true));

        return new ReportResult { HtmlPath = htmlPath, JsonPath = jsonPath, Verdict = verdict };
    }

    public static string ComputeVerdict(TestSession session)
    {
        if (session.Count(TestStatus.Rejected) > 0) return "REPROVADO";
        if (session.Count(TestStatus.Approved) == session.Total) return "APROVADO";
        return "INCOMPLETO";
    }

    private static string StatusText(TestStatus s) => s switch
    {
        TestStatus.Approved => "Aprovado",
        TestStatus.Rejected => "Reprovado",
        _ => "Pendente"
    };

    private static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Row(string label, string? value) =>
        $"<tr><th>{E(label)}</th><td>{E(string.IsNullOrWhiteSpace(value) ? "—" : value)}</td></tr>";

    private static string BuildHtml(string technician, string notes, DateTime now, string verdict,
        HardwareSpecs specs, BatteryReport? battery, TestSession session)
    {
        var sb = new StringBuilder();
        string verdictClass = verdict switch { "APROVADO" => "ok", "REPROVADO" => "bad", _ => "warn" };

        sb.Append("""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<title>Laudo Técnico - TechTest</title>
<style>
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', Arial, sans-serif; color: #1a1a2e; margin: 0; padding: 32px; background: #f4f5fb; }
  .page { max-width: 880px; margin: 0 auto; background: #fff; padding: 36px 40px; border-radius: 14px; box-shadow: 0 4px 24px rgba(20,20,60,.08); }
  header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid #6366f1; padding-bottom: 16px; margin-bottom: 22px; }
  h1 { margin: 0; font-size: 26px; } h1 small { display:block; font-size: 13px; color:#6b6b8a; font-weight: 400; margin-top: 4px; }
  h2 { font-size: 15px; text-transform: uppercase; letter-spacing: .06em; color: #6366f1; margin: 26px 0 8px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td { text-align: left; padding: 7px 10px; border-bottom: 1px solid #e6e6f2; vertical-align: top; }
  th { width: 32%; color: #5a5a78; font-weight: 600; }
  table.grid th { width: auto; background: #f0f0fa; }
  .badge { display: inline-block; padding: 3px 12px; border-radius: 999px; font-weight: 700; font-size: 12px; }
  .ok { background: #dcfce7; color: #15803d; } .bad { background: #fee2e2; color: #b91c1c; } .warn { background: #fef9c3; color: #a16207; }
  .verdict { font-size: 18px; padding: 8px 22px; }
  .sign { display: flex; gap: 40px; margin-top: 50px; }
  .sign div { flex: 1; border-top: 1px solid #1a1a2e; padding-top: 6px; font-size: 12px; color: #5a5a78; text-align: center; }
  footer { margin-top: 28px; font-size: 11px; color: #8a8aa5; text-align: center; }
  @media print { body { background: #fff; padding: 0; } .page { box-shadow: none; padding: 0; } }
</style>
</head>
<body><div class="page">
""");

        sb.Append($"<header><h1>Laudo Técnico de Bancada<small>TechTest — Diagnóstico e Controle de Qualidade</small></h1>");
        sb.Append($"<div style=\"text-align:right\"><span class=\"badge verdict {verdictClass}\">{E(verdict)}</span>");
        sb.Append($"<div style=\"font-size:12px;color:#6b6b8a;margin-top:6px\">{now:dd/MM/yyyy HH:mm}</div></div></header>");

        sb.Append("<h2>Identificação</h2><table>");
        sb.Append(Row("Fabricante", specs.Manufacturer));
        sb.Append(Row("Modelo", specs.Model));
        sb.Append(Row("Família / SKU", $"{specs.SystemFamily} / {specs.SystemSku}".Trim(' ', '/')));
        sb.Append(Row("Service Tag / Nº de Série", specs.ServiceTag));
        sb.Append(Row("Placa-mãe", $"{specs.BaseBoardManufacturer} {specs.BaseBoardProduct}".Trim()));
        sb.Append(Row("Nome da máquina", specs.MachineName));
        sb.Append(Row("Sistema operacional", specs.OSVersion));
        sb.Append(Row("Técnico responsável", technician));
        sb.Append("</table>");

        sb.Append("<h2>Hardware</h2><table>");
        sb.Append(Row("Processador", specs.CPU));
        sb.Append(Row("Memória RAM", specs.RAM));
        sb.Append(Row("Placa(s) de vídeo", string.Join(" | ", specs.GPUs.Select(g => g.Name))));
        sb.Append(Row("Tela", $"{specs.ScreenResolution} @ {(specs.ScreenRefreshRate > 0 ? specs.ScreenRefreshRate + " Hz" : "—")}"));
        sb.Append("</table>");

        sb.Append("<h2>Armazenamento — Saúde S.M.A.R.T.</h2>");
        if (specs.DiskHealth.Count > 0)
        {
            sb.Append("<table class=\"grid\"><tr><th>Disco</th><th>Tipo</th><th>Status</th><th>Saúde</th><th>Temperatura</th><th>Horas ligado</th></tr>");
            foreach (var d in specs.DiskHealth)
            {
                string cls = d.HealthStatus == "Saudável" ? "ok" : d.HealthStatus == "Desconhecido" ? "warn" : "bad";
                sb.Append($"<tr><td>{E(d.Name)}</td><td>{E(d.MediaType)}</td><td><span class=\"badge {cls}\">{E(d.HealthStatus)}</span></td>");
                sb.Append($"<td>{E(d.DisplayHealth)}</td><td>{E(d.DisplayTemp)}</td><td>{E(d.DisplayHours)}</td></tr>");
            }
            sb.Append("</table>");
        }
        else
        {
            sb.Append("<p style=\"font-size:13px;color:#6b6b8a\">Dados S.M.A.R.T. indisponíveis.</p>");
        }

        if (specs.StorageDevices.Count > 0)
        {
            sb.Append("<table class=\"grid\" style=\"margin-top:10px\"><tr><th>Volume</th><th>Tipo</th><th>Total</th><th>Livre</th><th>Formato</th></tr>");
            foreach (var s in specs.StorageDevices)
                sb.Append($"<tr><td>{E(s.Name)}</td><td>{E(s.Type)}</td><td>{E(s.FormattedTotalSize)}</td><td>{E(s.FormattedFreeSpace)}</td><td>{E(s.DriveFormat)}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<h2>Bateria</h2>");
        if (battery != null)
        {
            sb.Append("<table>");
            sb.Append(Row("Capacidade de projeto", battery.DesignCapacity));
            sb.Append(Row("Capacidade atual (carga total)", battery.FullChargeCapacity));
            sb.Append(Row("Saúde", battery.Health));
            sb.Append(Row("Ciclos de carga", battery.CycleCount));
            sb.Append(Row("Fabricante", battery.Manufacturer));
            sb.Append("</table>");
        }
        else
        {
            sb.Append("<p style=\"font-size:13px;color:#6b6b8a\">Bateria não detectada ou relatório indisponível.</p>");
        }

        sb.Append("<h2>Resultado dos Testes</h2><table class=\"grid\"><tr><th>Teste</th><th>Resultado</th></tr>");
        foreach (var id in TestSession.Order)
        {
            var st = session.GetStatus(id);
            string cls = st == TestStatus.Approved ? "ok" : st == TestStatus.Rejected ? "bad" : "warn";
            string icon = st == TestStatus.Approved ? "✅" : st == TestStatus.Rejected ? "❌" : "⏳";
            sb.Append($"<tr><td>{E(TestSession.TitleOf(id))}</td><td><span class=\"badge {cls}\">{icon} {E(StatusText(st))}</span></td></tr>");
        }
        sb.Append("</table>");

        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.Append("<h2>Observações</h2>");
            sb.Append($"<p style=\"font-size:13px;white-space:pre-wrap\">{E(notes)}</p>");
        }

        sb.Append("<div class=\"sign\"><div>Técnico responsável</div><div>Data</div></div>");
        sb.Append($"<footer>Gerado por TechTest em {now:dd/MM/yyyy HH:mm:ss}</footer>");
        sb.Append("</div></body></html>");

        return sb.ToString();
    }
}
