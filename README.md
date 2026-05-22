# TechTest

O **TechTest** é uma aplicação abrangente de diagnóstico e teste de hardware. Este repositório contém o código-fonte para ferramentas que permitem aos utilizadores testar vários componentes do seu computador ou dispositivo, garantindo que tudo funciona corretamente. O projeto disponibiliza duas versões da interface: uma aplicação moderna multiplataforma usando **.NET MAUI** e uma aplicação clássica em **Windows Forms**.

## 🚀 Funcionalidades Principais

A aplicação oferece um conjunto de testes dedicados para os seguintes componentes:

* **🎙️ Áudio e Microfone**: Testes de captura e reprodução de som com visualização de formas de onda.
* **🔋 Bateria**: Verificação do estado, percentagem e saúde da bateria.
* **📷 Câmara**: Diagnóstico da webcam ou câmaras integradas.
* **🖥️ Ecrã (Píxeis Mortos)**: Utilitário para deteção de píxeis mortos ou encravados no monitor.
* **⌨️ Teclado**: Verificação do funcionamento das teclas (com um layout interativo para portáteis).
* **🖱️ Touchpad**: Teste de precisão e resposta do painel tátil.
* **⚙️ Especificações do Sistema (Specs)**: Leitura detalhada das informações de hardware do dispositivo.

## 📁 Estrutura do Repositório

O projeto está dividido nas seguintes soluções principais:

* `TechTest.Maui/`: A versão principal e moderna da aplicação, construída com **.NET MAUI**. Suporta múltiplas plataformas (Windows, MacCatalyst, etc.) e utiliza o padrão MVVM (Model-View-ViewModel).
* `TechTest/`: A versão legada/clássica da aplicação, construída com **Windows Forms** (C#). Ideal para compatibilidade exclusiva com ambientes Windows mais antigos.
* `TechTest.Tests/`: Projeto que contém os testes unitários da aplicação para garantir a estabilidade e correção das funcionalidades.

## 🛠️ Tecnologias Utilizadas

* **C#** e **.NET** (versão suportada pelo MAUI)
* **.NET MAUI** (Multi-platform App UI)
* **Windows Forms** (WinForms)
* **XAML** (para a interface visual do MAUI)

## ⚙️ Pré-requisitos

Para compilar e executar este projeto localmente, irá necessitar de:

1.  [SDK do .NET](https://dotnet.microsoft.com/download) (versão 7.0 ou 8.0, conforme especificado no projeto).
2.  [Visual Studio 2022](https://visualstudio.microsoft.com/) (ou superior) com as seguintes *Workloads* instaladas:
    * Desenvolvimento de aplicações .NET Multi-platform App UI (.NET MAUI)
    * Desenvolvimento de aplicações para o ambiente de trabalho .NET (para o projeto WinForms)

## 🏃 Como Executar

1.  Faça o clone deste repositório:
    ```bash
    git clone https://github.com/seu-utilizador/techtest.git
    ```
2.  Abra o ficheiro da solução (`TechTest.slnx`) no Visual Studio.
3.  Defina o projeto de arranque (Startup Project) como `TechTest.Maui` (para a versão moderna) ou `TechTest` (para WinForms).
4.  Restaure os pacotes NuGet.
5.  Prima **F5** ou clique em "Start" para compilar e iniciar a aplicação.

## 🤝 Contribuição

Contribuições, problemas (*issues*) e pedidos de novas funcionalidades (*pull requests*) são bem-vindos. Sinta-se à vontade para verificar a página de *issues* caso encontre algum erro de hardware que a aplicação não esteja a detetar corretamente.

## 📄 Licença

Este projeto é disponibilizado "tal como está". Consulte o ficheiro de licença no repositório para obter mais detalhes sobre o uso e distribuição.
