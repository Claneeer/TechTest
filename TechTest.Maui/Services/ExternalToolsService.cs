using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TechTest.Maui.Services;

public class InternetTestConfig
{
    public int TimeoutMs { get; set; } = 3000;
    public List<string> DnsServers { get; set; } = new() { "8.8.8.8", "1.1.1.1" };
    public List<string> PingHosts { get; set; } = new() { "8.8.8.8", "1.1.1.1", "dns.google" };
    public List<string> HttpEndpoints { get; set; } = new()
    {
        "http://www.msftconnecttest.com/connecttest.txt",
        "https://www.google.com/generate_204"
    };
}

public class InternetDiagnosticResult
{
    public bool Success { get; set; }
    public string Summary { get; set; } = "";
    public string ConfigXmlPath { get; set; } = "";
}

public static class ExternalToolsService
{
    public static string EnsureToolsDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string toolsDir = Path.Combine(baseDir, "Tools");

        try
        {
            if (!Directory.Exists(toolsDir))
            {
                Directory.CreateDirectory(toolsDir);
            }
            return toolsDir;
        }
        catch
        {
            string localTools = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TechTest", "Tools");
            if (!Directory.Exists(localTools))
            {
                Directory.CreateDirectory(localTools);
            }
            return localTools;
        }
    }

    public static string GetInternetConfigXmlPath()
    {
        string toolsDir = EnsureToolsDirectory();
        return Path.Combine(toolsDir, "internet_config.xml");
    }

    public static InternetTestConfig EnsureAndLoadInternetConfigXml()
    {
        string xmlPath = GetInternetConfigXmlPath();
        var defaultConfig = new InternetTestConfig();

        try
        {
            if (!File.Exists(xmlPath))
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("InternetDiagnosticsConfig",
                        new XAttribute("version", "1.0"),
                        new XElement("Settings",
                            new XElement("TimeoutMs", defaultConfig.TimeoutMs)
                        ),
                        new XElement("DnsServers",
                            defaultConfig.DnsServers.Select(ip => new XElement("Server", new XAttribute("address", ip)))
                        ),
                        new XElement("PingTargets",
                            defaultConfig.PingHosts.Select(host => new XElement("Host", new XAttribute("address", host)))
                        ),
                        new XElement("ConnectivityEndpoints",
                            defaultConfig.HttpEndpoints.Select(url => new XElement("Endpoint", new XAttribute("url", url)))
                        )
                    )
                );

                doc.Save(xmlPath);
            }

            var loadedDoc = XDocument.Load(xmlPath);
            var root = loadedDoc.Element("InternetDiagnosticsConfig");
            if (root == null) return defaultConfig;

            var config = new InternetTestConfig();

            if (int.TryParse(root.Element("Settings")?.Element("TimeoutMs")?.Value, out int timeout) && timeout > 0)
            {
                config.TimeoutMs = timeout;
            }

            var dnsList = root.Element("DnsServers")?
                .Elements("Server")
                .Select(e => e.Attribute("address")?.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToList();
            if (dnsList is { Count: > 0 }) config.DnsServers = dnsList;

            var pingList = root.Element("PingTargets")?
                .Elements("Host")
                .Select(e => e.Attribute("address")?.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToList();
            if (pingList is { Count: > 0 }) config.PingHosts = pingList;

            var endpointList = root.Element("ConnectivityEndpoints")?
                .Elements("Endpoint")
                .Select(e => e.Attribute("url")?.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToList();
            if (endpointList is { Count: > 0 }) config.HttpEndpoints = endpointList;

            return config;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao processar internet_config.xml: {ex}");
            return defaultConfig;
        }
    }

    public static async Task<InternetDiagnosticResult> RunInternetDiagnosticsAsync()
    {
        string xmlPath = GetInternetConfigXmlPath();
        var config = EnsureAndLoadInternetConfigXml();
        var lines = new List<string>();
        bool anyPingOk = false;
        bool dnsOk = false;
        bool httpOk = false;

        await Task.Run(async () =>
        {
            // 1. Teste de Ping
            foreach (var host in config.PingHosts)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(host, config.TimeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        anyPingOk = true;
                        lines.Add($"✅ Ping {host}: {reply.RoundtripTime} ms");
                    }
                    else
                    {
                        lines.Add($"❌ Ping {host}: {reply.Status}");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"❌ Ping {host}: {ex.Message}");
                }
            }

            // 2. Teste de Resolução DNS
            try
            {
                var addresses = await Dns.GetHostAddressesAsync("www.microsoft.com");
                dnsOk = addresses != null && addresses.Length > 0;
                lines.Add(dnsOk
                    ? $"✅ DNS (www.microsoft.com): {addresses![0]} (Servidores config: {string.Join(", ", config.DnsServers)})"
                    : "❌ DNS: Sem resposta");
            }
            catch (Exception ex)
            {
                lines.Add($"❌ DNS: {ex.Message}");
            }

            // 3. Teste de Endpoints HTTP
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs) };
            foreach (var url in config.HttpEndpoints)
            {
                try
                {
                    using var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        httpOk = true;
                        lines.Add($"✅ HTTP {(int)response.StatusCode}: {url}");
                        break;
                    }
                    else
                    {
                        lines.Add($"⚠️ HTTP {(int)response.StatusCode}: {url}");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"❌ HTTP {url}: {ex.Message}");
                }
            }
        });

        bool overallSuccess = anyPingOk && (dnsOk || httpOk);
        lines.Add($"\nArquivo XML: {xmlPath}");

        return new InternetDiagnosticResult
        {
            Success = overallSuccess,
            Summary = string.Join("\n", lines),
            ConfigXmlPath = xmlPath
        };
    }

    public static (bool Started, string Message, string ExpectedDirectory) LaunchFurMark()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string toolsDir = EnsureToolsDirectory();

        string[] candidates =
        {
            Path.Combine(toolsDir, "FurMark.exe"),
            Path.Combine(toolsDir, "furmark.exe"),
            Path.Combine(toolsDir, "FurMark_GUI.exe"),
            Path.Combine(toolsDir, "FurMark", "FurMark.exe"),
            Path.Combine(toolsDir, "FurMark", "FurMark_GUI.exe"),
            Path.Combine(baseDir, "FurMark.exe"),
            Path.Combine(baseDir, "furmark.exe"),
            Path.Combine(baseDir, "FurMark_GUI.exe"),
            @"C:\Program Files\Geeks3D\FurMark\FurMark.exe",
            @"C:\Program Files (x86)\Geeks3D\FurMark\FurMark.exe",
            @"C:\Program Files\Geeks3D\FurMark2_x64\FurMark_GUI.exe"
        };

        string? exePath = candidates.FirstOrDefault(File.Exists);
        if (exePath == null)
        {
            return (false,
                $"Executável do FurMark não encontrado.\n\nColoque 'FurMark.exe' na pasta de ferramentas:\n{toolsDir}\nou na raiz do aplicativo:\n{baseDir}",
                toolsDir);
        }

        try
        {
            string workingDir = Path.GetDirectoryName(exePath) ?? baseDir;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            Process.Start(psi);
            return (true, $"FurMark iniciado a partir de: {exePath}", workingDir);
        }
        catch (Exception ex)
        {
            return (false, $"Falha ao iniciar o FurMark:\n{ex}", toolsDir);
        }
    }

    public static bool TryRunWindowsAppRuntimeInstallerIfPresent(bool silent = true)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string toolsDir = EnsureToolsDirectory();

            string[] candidates =
            {
                Path.Combine(baseDir, "WindowsAppRuntimeInstall-x64.exe"),
                Path.Combine(toolsDir, "WindowsAppRuntimeInstall-x64.exe"),
                Path.Combine(baseDir, "Redist", "WindowsAppRuntimeInstall-x64.exe")
            };

            string? installerPath = candidates.FirstOrDefault(File.Exists);
            if (installerPath == null) return false;

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = silent ? "--quiet --force" : "",
                UseShellExecute = true,
                CreateNoWindow = silent
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(30000);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Falha ao executar WindowsAppRuntimeInstall-x64.exe: {ex}");
            return false;
        }
    }
}

