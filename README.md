# 💻 TechTest — Suíte de Diagnóstico e Controle de Qualidade (QA) para Notebooks

## 📌 O que o projeto faz
O **TechTest** é uma aplicação multiplataforma de diagnóstico de hardware e validação funcional para notebooks e desktops. O sistema centraliza testes interativos de periféricos, telemetria de componentes internos, verificação de saúde de armazenamento/bateria, identificação de drivers ausentes e emissão de laudo técnico de aprovação (QA).

---

## 🔄 Qual fluxo ou problema ele simula e resolve
Em ambientes de **assistência técnica, refurbishing (recondicionamento de seminovos), triagem de RMA e laboratórios de TI**, o teste de um notebook costuma ser manual, fragmentado e dependente de múltiplos softwares de terceiros ou sites avulsos. Isso gera retrabalho e deixa passar defeitos intermitentes (como teclas com duplo clique, zonas mortas no touchpad ou SSDs com degradação oculta).

O **TechTest** resolve esse gargalo padronizando um **fluxo guiado de inspeção de bancada**:
1. **Identificação e Triagem:** Leitura instantânea das especificações da máquina, Serial Number (Service Tag) e saúde física do disco (S.M.A.R.T.).
2. **Validação de Periféricos de Entrada/Saída:** Baterias de testes visuais e auditivos para tela, teclado, touchpad, webcam, microfone e alto-falantes.
3. **Pós-Formatação e Drivers:** Detecção de dispositivos com falha no Gerenciador de Dispositivos e acionamento automatizado de ferramentas OEM (Dell, Lenovo, HP, etc.).
4. **Certificação (Pass/Fail):** Registro de aprovação ou reprovação em cada etapa (`ApprovalBar`) para geração de um relatório técnico final da sessão (`TestSession` / `ReportService`).

---

## ⚙️ Quais funcionalidades existem

* **📊 Especificações e Saúde S.M.A.R.T. (`SpecsPage`):** Coleta detalhada de CPU, GPU, RAM, Placa-Mãe, BIOS/Serial e leitura de indicadores físicos dos discos (Saúde em %, Temperatura e Horas de Uso).
* **⌨️ Teste de Teclado com Detecção de Chattering (`KeyboardTestPage`):** Renderização visual do layout do teclado em Canvas, barra superior com histórico das últimas teclas pressionadas, contador de *Key Rollover* (teclas simultâneas) e alerta em milissegundos para *Key Chattering* (duplo disparo involuntário por falha mecânica).
* **🖱️ Scanner de Superfície de Touchpad (`TouchpadTestPage`):** Mapeamento em grade (*Dead Zone Grid Scanner*) para validar 100% da área útil de toque, checklist de gestos (cliques esquerdo/direito, scroll multi-toque) e detector de *Ghost Touch* (toque fantasma).
* **🔋 Diagnóstico de Bateria (`BatteryTestPage`):** Monitoramento gráfico de carga, status de alimentação AC/DC, tensão, taxa de descarga, capacidade de projeto (*Design Capacity*) vs. capacidade real (*Full Charge Capacity*) e cálculo de nível de desgaste (*Wear Level*).
* **🔊 Teste de Áudio Estéreo (`AudioTestPage`):** Geração de ondas sonoras e teste independente de canais (Esquerdo, Direito e Ambos) com animação em Canvas (`SpeakerCanvasView`).
* **🎙️ Teste de Microfone com Osciloscópio (`MicrophoneTestPage`):** Captura de áudio em tempo real com renderização da forma de onda (`WaveformCanvasView`), medidor de pico em dB e loopback para audição.
* **📷 Teste de Webcam (`CameraTestPage`):** Inicialização e preview de vídeo em tempo real das câmeras conectadas ao dispositivo.
* **🖥️ Inspeção de Dead Pixel (`DeadPixelTestPage`):** Ciclos em tela cheia com cores sólidas primárias e secundárias para identificação visual de *dead pixels*, *stuck pixels* ou manchas de *backlight bleeding*.
* **🛠️ Atualizador de Drivers OEM (`DriverUpdatePage`):** Identificação automática do fabricante via WMI/BIOS, listagem de drivers com erro no sistema e integração com utilitários oficiais das fabricantes via CLI/`winget` (`OemDriverUpdater`).
* **✅ Barra de Aprovação e Sessão de Laudo (`ApprovalBar` & `ReportService`):** Controle persistente do estado de cada teste (*Aprovado*, *Reprovado*, *Pendente*) com exportação de laudo técnico consolidado.

---

## 🚀 Quais tecnologias foram usadas

* **Linguagem:** C# 12 / .NET 8 e .NET 9
* **Frameworks de Interface:**
  * **.NET MAUI (WinUI 3 & MacCatalyst):** Interface moderna multiplataforma com desenho acelerado via `Microsoft.Maui.Graphics` (`ICanvas` / `GraphicsView`).
  * **Windows Forms (`TechTest` legado):** Versão desktop leve exclusiva para Windows com GDI+.
* **Arquitetura e Padrões:** MVVM (*Model-View-ViewModel*), Injeção de Dependência (DI) nativa com `Microsoft.Extensions.DependencyInjection`, Serviços Abstratos por Plataforma (`IHardwareInfoService`, `IBatteryService`, `IAudioCaptureService`, `IAudioPlaybackService`).
* **APIs de Sistema e Hardware:**
  * **Windows:** WMI (`System.Management` — `Win32_*`, `MSFT_PhysicalDisk`), `NAudio` / Windows Audio Session API (WASAPI), Win32 Power APIs e `winget` CLI.
  * **macOS (MacCatalyst):** `IOKit`, `system_profiler`, `AVFoundation` (captura e reprodução de áudio nativo).
* **Testes Automatizados:** xUnit (`TechTest.Tests`).

---

## ▶️ Como executar o projeto

### Pré-requisitos
* [.NET SDK](https://dotnet.microsoft.com/download) instalado (com o workload do `.NET MAUI` habilitado: `dotnet workload install maui`).
* **Sistema Operacional:** Windows 10/11 (versão 10.0.19041.0 ou superior) ou macOS (para build MacCatalyst).

### Executando a versão .NET MAUI (Recomendada)
No terminal, a partir da raiz do repositório:

```bash
# 1. Restaurar pacotes NuGet
dotnet restore TechTest.slnx

# 2. Compilar e rodar no Windows (Unpackaged)
dotnet build TechTest.Maui/TechTest.Maui.csproj -f net8.0-windows10.0.19041.0
dotnet run --project TechTest.Maui/TechTest.Maui.csproj -f net8.0-windows10.0.19041.0
