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
* **⌨️ Teste de Teclado com Detecção de Chattering (`KeyboardTestPage`):** Layout ABNT2 desenhado em SkiaSharp que escala para 95% da área disponível, diferencia Shift/Ctrl/Alt esquerdo e direito (AltGr), mapeia toda a pontuação ABNT2 (Ç, `/ ?`, `\ |`, etc.), impede que Tab/Espaço/Enter/Alt/F10 roubem o foco, e exibe histórico, *Key Rollover* e alerta em ms para *Key Chattering* (duplo disparo por falha mecânica).
* **🖱️ Scanner Absoluto de Touchpad (`TouchpadTestPage`):** Lê as **coordenadas físicas absolutas** do Precision Touchpad via Win32 *Raw Input* (HID `UsagePage 0x0D / Usage 0x05`) e normaliza (`LogicalMin..LogicalMax` → `0.0..1.0`) para uma grade 15x10 de zonas mortas — independente da posição do cursor e ignorando mouses USB. Alterna para o **Modo Cursor (PS/2 legado)**, que prende o cursor ao canvas (ESC libera). Inclui checklist de gestos e detector de *Ghost Touch*.
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
  * **.NET MAUI (WinUI 3 & MacCatalyst):** Interface moderna multiplataforma com desenho acelerado via **SkiaSharp** (`SKCanvasView`) e `CommunityToolkit.Maui` (câmera).
  * **Windows Forms (`TechTest` legado):** Versão desktop leve exclusiva para Windows com GDI+.
* **Arquitetura e Padrões:** MVVM (*Model-View-ViewModel*), Injeção de Dependência (DI) nativa com `Microsoft.Extensions.DependencyInjection`, Serviços Abstratos por Plataforma (`IHardwareInfoService`, `IBatteryService`, `IAudioCaptureService`, `IAudioPlaybackService`).
* **APIs de Sistema e Hardware:**
  * **Windows:** WMI (`System.Management` — `Win32_*`, `MSFT_PhysicalDisk`), **Win32 Raw Input / HID** (`user32.dll`, `hid.dll`), `NAudio` / WASAPI, Win32 Power APIs e `winget` CLI.
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

### Fluxo de uso
1. Na tela inicial, clique em **▶️ Iniciar Diagnóstico Sequencial** para percorrer todos os testes (aprovar avança para o próximo) ou abra cada card individualmente — cada card mostra o badge *Aprovado / Reprovado / Pendente*.
2. Ao final, clique em **📄 Exportar Laudo Técnico** para gerar o laudo em HTML (pronto para impressão) e JSON em `Documentos\TechTest\Laudos`.

---

## 📦 Como publicar (versão portátil para pendrive)

```bash
dotnet publish TechTest.Maui/TechTest.Maui.csproj -f net8.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o ./Pendrive_Maui
```

Copie a pasta `Pendrive_Maui` para o pendrive e execute `TechTest.Maui.exe` em qualquer Windows 10/11 x64, sem instalar runtimes. (Opcional: coloque `FurMark.exe` na mesma pasta para habilitar o botão de stress test.)

---

## 🎯 O que o projeto demonstra tecnicamente

* **Interop nativo no .NET MAUI:** P/Invoke para `user32.dll`/`hid.dll`, subclassing de `WndProc` (`WM_INPUT`), parsing de descritores HID (`HidP_GetValueCaps`, `HidP_GetUsageValue`).
* **Renderização customizada com SkiaSharp:** layout escalável por transformação de canvas, estados visuais e animações.
* **Resiliência contra hardware ausente:** enumeração WinRT antes de criar a câmera, sem `Permissions` em apps *Unpackaged*, e handlers globais de exceções (`UnhandledException` / `UnobservedTaskException`).
* **Integração com o sistema:** WMI, S.M.A.R.T., utilitários OEM e `winget`.
* **Domínio de QA de bancada:** sessão de testes, modo sequencial e laudo técnico exportável.
