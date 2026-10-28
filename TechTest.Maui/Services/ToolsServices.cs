using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace TechTest.Maui.Services;

/// <summary>Estrutura de diretórios controlada pela aplicação (Tools\FurMark, Tools\Config).</summary>
public interface IToolsPathProvider
{
    string ToolsRoot { get; }
    string FurMarkDirectory { get; }
    string ConfigDirectory { get; }
    string NetworkConfigPath { get; }

    /// <summary>Cria (idempotente) todos os diretórios necessários.</summary>
    void EnsureDirectories();
}

public sealed class ToolsPathProvider : IToolsPathProvider
{
    public string ToolsRoot { get; } = Path.Combine(FileSystem.AppDataDirectory, "Tools");
    public string FurMarkDirectory => Path.Combine(ToolsRoot, "FurMark");
    public string ConfigDirectory => Path.Combine(ToolsRoot, "Config");
    public string NetworkConfigPath => Path.Combine(ConfigDirectory, "network_config.xml");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(FurMarkDirectory);
        Directory.CreateDirectory(ConfigDirectory);
    }
}

// ───────────────────────────── Rede / XML ─────────────────────────────

public sealed class NetworkConfig
{
    public int PingCount { get; set; } = 4;
    public int PingTimeoutMs { get; set; } = 2000;
    public List<string> PingHosts { get; set; } = new() { "8.8.8.8", "1.1.1.1" };
    public List<string> DnsServers { get; set; } = new() { "8.8.8.8", "1.1.1.1" };
    public string DnsTestHostname { get; set; } = "www.microsoft.com";
    public string HttpEndpoint { get; set; } = "http://www.msftconnecttest.com/connecttest.txt";
    public int HttpTimeoutMs { get; set; } = 5000;
    public List<int> TcpPorts { get; set; } = new() { 53, 80, 443 };
    public bool ProxyEnabled { get; set; }
    public string ProxyAddress { get; set; } = "";
    public int ProxyPort { get; set; } = 8080;
    public string WifiSsid { get; set; } = "";
}

public interface INetworkConfigService
{
    /// <summary>Cria o XML padrão se não existir e devolve o caminho.</summary>
    Task<string> EnsureConfigAsync(CancellationToken ct = default);
    Task SaveAsync(NetworkConfig config, CancellationToken ct = default);

    /// <summary>Lê e valida. Lança <see cref="InvalidDataException"/> com mensagem amigável se o XML for inválido.</summary>
    Task<NetworkConfig> LoadAsync(CancellationToken ct = default);
}

public sealed class NetworkConfigService : INetworkConfigService
{
    private const string Root = "TechTestNetworkConfig";
    private readonly IToolsPathProvider _paths;
    private readonly SemaphoreSlim _io = new(1, 1);

    public NetworkConfigService(IToolsPathProvider paths) => _paths = paths;

    public async Task<string> EnsureConfigAsync(CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _paths.EnsureDirectories();
            if (!File.Exists(_paths.NetworkConfigPath))
                await WriteUnsafeAsync(new NetworkConfig(), ct).ConfigureAwait(false);
            return _paths.NetworkConfigPath;
        }
        finally { _io.Release(); }
    }

    public async Task SaveAsync(NetworkConfig config, CancellationToken ct = default)
    {
        Validate(config);
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _paths.EnsureDirectories();
            await WriteUnsafeAsync(config, ct).ConfigureAwait(false);
        }
        finally { _io.Release(); }
    }

    public async Task<NetworkConfig> LoadAsync(CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct).ConfigureAwait(false);

        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            XDocument doc;
            try
            {
                await using var fs = File.OpenRead(_paths.NetworkConfigPath);
                doc = await XDocument.LoadAsync(fs, LoadOptions.None, ct).ConfigureAwait(false);
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException($"XML malformado ({ex.Message}). Apague o arquivo para regenerar o padrão.", ex);
            }

            var cfg = Parse(doc);
            Validate(cfg);
            return cfg;
        }
        finally { _io.Release(); }
    }

    // Escrita atômica: grava em .tmp e substitui, evitando XML truncado se o app for fechado no meio.
    private async Task WriteUnsafeAsync(NetworkConfig c, CancellationToken ct)
    {
        var doc = Build(c);
        string tmp = _paths.NetworkConfigPath + ".tmp";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await doc.SaveAsync(fs, SaveOptions.None, ct).ConfigureAwait(false);
        }
        File.Move(tmp, _paths.NetworkConfigPath, overwrite: true);
    }

    private static XDocument Build(NetworkConfig c) =>
        new(new XDeclaration("1.0", "utf-8", "yes"),
            new XComment(" Configuração de rede do TechTest. Edite com cuidado; valores inválidos são rejeitados. "),
            new XElement(Root, new XAttribute("version", "1"),
                new XElement("Ping",
                    new XElement("Count", c.PingCount),
                    new XElement("TimeoutMs", c.PingTimeoutMs),
                    new XElement("Hosts", c.PingHosts.Select(h => new XElement("Host", h)))),
                new XElement("Dns",
                    new XElement("TestHostname", c.DnsTestHostname),
                    new XElement("Servers", c.DnsServers.Select(s => new XElement("Server", s)))),
                new XElement("Http",
                    new XElement("Endpoint", c.HttpEndpoint),
                    new XElement("TimeoutMs", c.HttpTimeoutMs)),
                new XElement("Tcp",
                    new XElement("Ports", c.TcpPorts.Select(p => new XElement("Port", p)))),
                new XElement("Proxy", new XAttribute("enabled", c.ProxyEnabled ? "true" : "false"),
                    new XElement("Address", c.ProxyAddress),
                    new XElement("Port", c.ProxyPort)),
                new XElement("Wifi", new XElement("Ssid", c.WifiSsid))));

    private static NetworkConfig Parse(XDocument doc)
    {
        var root = doc.Root;
        if (root == null || root.Name.LocalName != Root)
            throw new InvalidDataException($"Elemento raiz <{Root}> não encontrado.");

        static int I(XElement? e, int fallback) =>
            e != null && int.TryParse(e.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        var def = new NetworkConfig();
        return new NetworkConfig
        {
            PingCount = I(root.Element("Ping")?.Element("Count"), def.PingCount),
            PingTimeoutMs = I(root.Element("Ping")?.Element("TimeoutMs"), def.PingTimeoutMs),
            PingHosts = root.Element("Ping")?.Element("Hosts")?.Elements("Host").Select(e => e.Value.Trim()).Where(s => s.Length > 0).ToList() ?? def.PingHosts,
            DnsTestHostname = root.Element("Dns")?.Element("TestHostname")?.Value.Trim() ?? def.DnsTestHostname,
            DnsServers = root.Element("Dns")?.Element("Servers")?.Elements("Server").Select(e => e.Value.Trim()).Where(s => s.Length > 0).ToList() ?? def.DnsServers,
            HttpEndpoint = root.Element("Http")?.Element("Endpoint")?.Value.Trim() ?? def.HttpEndpoint,
            HttpTimeoutMs = I(root.Element("Http")?.Element("TimeoutMs"), def.HttpTimeoutMs),
            TcpPorts = root.Element("Tcp")?.Element("Ports")?.Elements("Port").Select(e => I(e, -1)).ToList() ?? def.TcpPorts,
            ProxyEnabled = string.Equals((string?)root.Element("Proxy")?.Attribute("enabled"), "true", StringComparison.OrdinalIgnoreCase),
            ProxyAddress = root.Element("Proxy")?.Element("Address")?.Value.Trim() ?? "",
            ProxyPort = I(root.Element("Proxy")?.Element("Port"), def.ProxyPort),
            WifiSsid = root.Element("Wifi")?.Element("Ssid")?.Value.Trim() ?? ""
        };
    }

    private static void Validate(NetworkConfig c)
    {
        var errors = new List<string>();
        if (c.PingCount is < 1 or > 20) errors.Add("Ping/Count deve estar entre 1 e 20.");
        if (c.PingTimeoutMs is < 100 or > 30000) errors.Add("Ping/TimeoutMs deve estar entre 100 e 30000.");
        if (c.PingHosts.Count == 0) errors.Add("Informe ao menos um <Host> em Ping/Hosts.");
        if (c.HttpTimeoutMs is < 100 or > 60000) errors.Add("Http/TimeoutMs deve estar entre 100 e 60000.");
        if (!Uri.TryCreate(c.HttpEndpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors.Add("Http/Endpoint deve ser uma URL http(s) absoluta.");
        if (c.TcpPorts.Any(p => p is < 1 or > 65535)) errors.Add("Tcp/Ports contém porta fora de 1-65535.");
        foreach (var s in c.DnsServers)
            if (!System.Net.IPAddress.TryParse(s, out _)) errors.Add($"Dns/Server '{s}' não é um IP válido.");
        if (c.ProxyEnabled && (string.IsNullOrWhiteSpace(c.ProxyAddress) || c.ProxyPort is < 1 or > 65535))
            errors.Add("Proxy habilitado exige Address e Port válidos.");

        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
}

// ───────────────────────────── FurMark ─────────────────────────────

public sealed record ToolLaunchResult(bool Success, string Message, int? ProcessId = null);

public interface IFurMarkService
{
    /// <summary>Caminho do executável encontrado (ou null).</summary>
    string? ResolveExecutable();

    /// <summary>Prepara diretórios + XML de rede e inicia o FurMark. Nunca lança: devolve o resultado.</summary>
    Task<ToolLaunchResult> LaunchAsync(string? extraArguments = null, CancellationToken ct = default);
}

public sealed class FurMarkService : IFurMarkService
{
    // FurMark 2.x (CLI): ajuste conforme a versão distribuída em Tools\FurMark.
    // Referência: furmark.exe --demo furmark-gl --width 1280 --height 720 --max-time 60
    private const string DefaultArguments = "--demo furmark-gl --width 1280 --height 720 --max-time 60";

    private static readonly string[] ExecutableNames =
    {
        "furmark.exe", "FurMark.exe", "FurMark_GUI.exe", Path.Combine("FurMark_win64", "furmark.exe")
    };

    private readonly IToolsPathProvider _paths;
    private readonly INetworkConfigService _network;

    public FurMarkService(IToolsPathProvider paths, INetworkConfigService network)
    {
        _paths = paths;
        _network = network;
    }

    public string? ResolveExecutable()
    {
        var roots = new List<string> { _paths.FurMarkDirectory };
        foreach (var pf in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var p = Environment.GetFolderPath(pf);
            if (!string.IsNullOrEmpty(p))
            {
                roots.Add(Path.Combine(p, "Geeks3D", "FurMark2"));
                roots.Add(Path.Combine(p, "Geeks3D", "Benchmarks", "FurMark"));
            }
        }

        foreach (var root in roots.Where(Directory.Exists))
            foreach (var name in ExecutableNames)
            {
                var path = Path.Combine(root, name);
                if (File.Exists(path)) return path;
            }

        return null;
    }

    public async Task<ToolLaunchResult> LaunchAsync(string? extraArguments = null, CancellationToken ct = default)
    {
        try
        {
            _paths.EnsureDirectories();
            await _network.EnsureConfigAsync(ct).ConfigureAwait(false); // garante o XML de rede junto da automação

            var exe = ResolveExecutable();
            if (exe == null)
                return new ToolLaunchResult(false,
                    $"FurMark não encontrado. Copie os arquivos do FurMark para:\n{_paths.FurMarkDirectory}");

#if WINDOWS
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.IsNullOrWhiteSpace(extraArguments) ? DefaultArguments : extraArguments,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            return process == null
                ? new ToolLaunchResult(false, "O Windows não iniciou o processo do FurMark.")
                : new ToolLaunchResult(true, "FurMark iniciado.", process.Id);
#else
            return new ToolLaunchResult(false, "FurMark só é suportado no Windows.");
#endif
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ToolLaunchResult(false, "Elevação (UAC) cancelada pelo usuário.");
        }
        catch (OperationCanceledException)
        {
            return new ToolLaunchResult(false, "Operação cancelada.");
        }
        catch (Exception ex)
        {
            return new ToolLaunchResult(false, $"Falha ao iniciar o FurMark: {ex.Message}");
        }
    }
}
