using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace TechTest.Maui.Services;

/// <summary>Formata <see cref="HardwareSpecs"/> e informações de bateria para texto legível (tabulado) ou JSON. Tolerante a nulos.</summary>
public static class HardwareReportFormatter
{
    private const string Na = "N/D";

    private static string V(string? s) =>
        string.IsNullOrWhiteSpace(s) || s == "Carregando..." || s == "—" ? Na : s.Trim();

    public static string CleanSerialNumber(string? rawSerial)
    {
        if (string.IsNullOrWhiteSpace(rawSerial))
            return string.Empty;

        string cleaned = rawSerial.Trim();
        string[] invalidPlaceholders =
        {
            "—", "Carregando...", "Não disponível", "Desconhecido", "Não informado",
            "To be filled by O.E.M.", "Default string", "System Serial Number", "None", "N/A", "N/D"
        };

        foreach (var placeholder in invalidPlaceholders)
        {
            if (cleaned.Equals(placeholder, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
        }

        return cleaned;
    }

    public static string ToText(
        HardwareSpecs? s,
        BatteryStatus? batteryStatus = null,
        BatteryReport? batteryReport = null)
    {
        if (s == null) return "Informações de hardware indisponíveis.";

        var sb = new StringBuilder();
        void Row(string k, string? v) => sb.AppendLine($"{k,-26}: {V(v)}");

        sb.AppendLine("=== ESPECIFICAÇÕES DO SISTEMA ===");
        sb.AppendLine($"Gerado em                 : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("[Identificação]");
        Row("Fabricante", s.Manufacturer);
        Row("Modelo", s.Model);
        Row("Família", s.SystemFamily);
        Row("SKU", s.SystemSku);
        Row("Número de Série (SN)", s.ServiceTag);
        Row("Serial da BIOS", s.BiosSerial);
        Row("Placa-mãe (Fabricante)", s.BaseBoardManufacturer);
        Row("Placa-mãe (Modelo)", s.BaseBoardProduct);
        Row("Placa-mãe (Serial)", s.BaseBoardSerial);
        sb.AppendLine();

        sb.AppendLine("[Sistema e Processamento]");
        Row("Sistema Operacional (SO)", s.OSVersion);
        Row("Nome da Máquina", s.MachineName);
        Row("Processador (CPU)", s.CPU);
        Row("Memória RAM", s.RAM);
        sb.AppendLine();

        sb.AppendLine("[Vídeo e Tela]");
        Row("Resolução", s.ScreenResolution);
        Row("Taxa de Atualização", s.ScreenRefreshRate > 0 ? $"{s.ScreenRefreshRate} Hz" : null);
        if (s.GPUs != null && s.GPUs.Count > 0)
        {
            foreach (var g in s.GPUs)
                Row("GPU", $"{g.Name} (driver {V(g.DriverVersion)}, {(g.VideoMemoryBytes > 0 ? $"{g.VideoMemoryBytes / 1024.0 / 1024 / 1024:F1} GB" : Na)})");
        }
        else
        {
            Row("GPU", Na);
        }
        sb.AppendLine();

        sb.AppendLine("[Armazenamento]");
        if (s.StorageDevices != null && s.StorageDevices.Count > 0)
        {
            foreach (var d in s.StorageDevices)
                Row($"Unidade ({d.Name})", $"{d.Type} {d.DriveFormat} - {d.FormattedUsedSpace} / {d.FormattedTotalSize} ({d.FormattedFreeSpace} livre)");
        }
        else
        {
            Row("Unidades", Na);
        }

        if (s.DiskHealth != null && s.DiskHealth.Count > 0)
        {
            foreach (var h in s.DiskHealth)
                Row($"S.M.A.R.T. ({h.Name})", $"{h.HealthStatus} | {h.Summary}");
        }
        sb.AppendLine();

        sb.AppendLine("[Bateria e Energia]");
        if (batteryStatus != null && !batteryStatus.HasBattery)
        {
            Row("Status da Bateria", BatteryStatus.NoBatteryMessage);
            Row("Fonte de Energia", batteryStatus.PowerSource);
        }
        else if (batteryStatus != null || batteryReport != null)
        {
            if (batteryStatus != null)
            {
                Row("Status", batteryStatus.StatusText);
                Row("Carga Atual", $"{batteryStatus.ChargePercent}%");
                Row("Fonte de Energia", batteryStatus.PowerSource);
            }
            if (batteryReport != null)
            {
                Row("Saúde da Bateria", batteryReport.Health);
                Row("Capacidade de Projeto", batteryReport.DesignCapacity);
                Row("Capacidade Carga Total", batteryReport.FullChargeCapacity);
                Row("Ciclos de Carga", batteryReport.CycleCount);
                Row("Fabricante da Bateria", batteryReport.Manufacturer);
            }
        }
        else
        {
            Row("Informações da Bateria", Na);
        }

        sb.AppendLine("=================================");
        return sb.ToString();
    }

    public static string ToJson(
        HardwareSpecs? s,
        BatteryStatus? batteryStatus = null,
        BatteryReport? batteryReport = null)
    {
        var payload = new
        {
            generatedAt = DateTime.Now,
            manufacturer = V(s?.Manufacturer),
            model = V(s?.Model),
            serialNumber = V(s?.ServiceTag),
            biosSerial = V(s?.BiosSerial),
            baseBoard = new { manufacturer = V(s?.BaseBoardManufacturer), product = V(s?.BaseBoardProduct), serial = V(s?.BaseBoardSerial) },
            os = V(s?.OSVersion),
            machineName = V(s?.MachineName),
            cpu = V(s?.CPU),
            ram = V(s?.RAM),
            gpus = s?.GPUs?.Select(g => new { g.Name, g.DriverVersion, g.VideoMemoryBytes }),
            storage = s?.StorageDevices?.Select(d => new { d.Name, d.Type, d.DriveFormat, d.TotalBytes, d.FreeBytes }),
            battery = new
            {
                hasBattery = batteryStatus?.HasBattery,
                status = V(batteryStatus?.StatusText),
                chargePercent = batteryStatus?.ChargePercent,
                powerSource = V(batteryStatus?.PowerSource),
                health = V(batteryReport?.Health),
                designCapacity = V(batteryReport?.DesignCapacity),
                fullChargeCapacity = V(batteryReport?.FullChargeCapacity),
                cycleCount = V(batteryReport?.CycleCount),
                manufacturer = V(batteryReport?.Manufacturer)
            }
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
