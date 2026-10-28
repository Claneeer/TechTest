using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

#if WINDOWS
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Maui.Platform;
#else
using CommunityToolkit.Maui.Views;
#endif

namespace TechTest.Maui.Views
{
    public partial class CameraTestPage : ContentPage
    {
        private readonly SemaphoreSlim _cameraLock = new(1, 1);
        private bool _userRequestedStart;
        private string? _activeDeviceId;

#if WINDOWS
        private MediaCapture? _mediaCapture;
        private MediaFrameReader? _frameReader;
        private MediaPlayerElement? _mediaPlayerElement;
        private MediaPlayer? _mediaPlayer;
        private Microsoft.UI.Xaml.Controls.Grid? _nativeHostGrid;
        private Microsoft.UI.Xaml.Controls.Image? _previewImage;
        private SoftwareBitmapSource? _bitmapSource;
        private SoftwareBitmap? _backBuffer;
        private int _isRenderingFrame;
        private DeviceWatcher? _deviceWatcher;
        private ContentPanel? _attachedPanel;
#else
        private CameraView? _camView;
        private CancellationTokenSource? _cts;
#endif

        public CameraTestPage()
        {
            InitializeComponent();
            TechTest.Maui.Controls.ApprovalBar.Attach(this, TechTest.Maui.Services.TestId.Camera);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
#if WINDOWS
            StartDeviceWatcher();
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _userRequestedStart = false;
#if WINDOWS
            StopDeviceWatcher();
#endif
            StopCamera();
        }

        private async void OnStartCameraClicked(object sender, EventArgs e)
        {
            _userRequestedStart = true;
            await InitializeCameraAsync();
        }

        private async Task InitializeCameraAsync(string? preferredDeviceId = null)
        {
            if (!await _cameraLock.WaitAsync(0)) return;

            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BtnStartCamera.IsEnabled = false;
                    LblPermission.Text = "A verificar...";
                    LblPermission.TextColor = Color.FromArgb("#EAB308");
                });

                PermissionStatus permissionStatus = PermissionStatus.Granted;
                try
                {
                    permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (permissionStatus != PermissionStatus.Granted)
                    {
                        permissionStatus = await Permissions.RequestAsync<Permissions.Camera>();
                    }
                }
                catch (Exception permEx)
                {
                    Debug.WriteLine($"Aviso permissão câmera (Unpackaged Win32): {permEx.Message}");
                    permissionStatus = PermissionStatus.Granted;
                }

                if (permissionStatus != PermissionStatus.Granted)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        LblPermission.Text = "Negada";
                        LblPermission.TextColor = Color.FromArgb("#EF4444");
                        BtnStartCamera.IsEnabled = true;
                        await DisplayAlert("Permissão Negada", "O acesso à câmera foi bloqueado nas configurações de privacidade do Windows.", "OK");
                    });
                    return;
                }

#if WINDOWS
                var videoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                if (videoDevices == null || videoDevices.Count == 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LblPermission.Text = "Sem Hardware";
                        LblPermission.TextColor = Color.FromArgb("#EF4444");
                        LblDevice.Text = "Nenhuma Webcam";
                        LblPlaceholderTitle.Text = "Nenhuma webcam detectada";
                        LblPlaceholderDesc.Text = "Conecte uma webcam USB ou verifique se a câmera integrada está habilitada.";
                        BtnStartCamera.IsEnabled = true;
                    });
                    return;
                }

                var selectedDevice = (!string.IsNullOrEmpty(preferredDeviceId)
                    ? videoDevices.FirstOrDefault(d => d.Id == preferredDeviceId)
                    : null) ?? videoDevices.FirstOrDefault();

                if (selectedDevice == null)
                {
                    MainThread.BeginInvokeOnMainThread(() => BtnStartCamera.IsEnabled = true);
                    return;
                }

                _activeDeviceId = selectedDevice.Id;

                var nativeWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                var dispatcherQueue = nativeWindow?.DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                async Task RunOnDispatcherAsync()
                {
                    try
                    {
                        await CleanupNativeCameraResourcesAsync();

                        LblDevice.Text = selectedDevice.Name ?? "Webcam Integrada";

                        _mediaCapture = new MediaCapture();
                        _mediaCapture.Failed += OnMediaCaptureFailed;

                        // MemoryPreference.Cpu evita tela preta por incompatibilidade de textura D3D11 em notebooks híbridos (iGPU + dGPU)
                        var settings = new MediaCaptureInitializationSettings
                        {
                            VideoDeviceId = selectedDevice.Id,
                            StreamingCaptureMode = StreamingCaptureMode.Video,
                            SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                            MemoryPreference = MediaCaptureMemoryPreference.Cpu
                        };

                        try
                        {
                            await _mediaCapture.InitializeAsync(settings);
                        }
                        catch
                        {
                            _mediaCapture.Dispose();
                            _mediaCapture = new MediaCapture();
                            _mediaCapture.Failed += OnMediaCaptureFailed;

                            var sharedSettings = new MediaCaptureInitializationSettings
                            {
                                VideoDeviceId = selectedDevice.Id,
                                StreamingCaptureMode = StreamingCaptureMode.Video,
                                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                                MemoryPreference = MediaCaptureMemoryPreference.Cpu
                            };
                            await _mediaCapture.InitializeAsync(sharedSettings);
                        }

                        var frameSource = _mediaCapture.FrameSources.Values
                            .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color &&
                                                 s.Info.MediaStreamType == MediaStreamType.VideoPreview)
                            ?? _mediaCapture.FrameSources.Values
                                .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color &&
                                                     s.Info.MediaStreamType == MediaStreamType.VideoRecord)
                            ?? _mediaCapture.FrameSources.Values
                                .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                            ?? _mediaCapture.FrameSources.Values.FirstOrDefault();

                        if (frameSource == null)
                        {
                            throw new InvalidOperationException("Nenhuma fonte de vídeo (MediaFrameSource) encontrada para esta câmera.");
                        }

                        // Seleciona preferencialmente formato >= 640x480 se o formato atual for inválido
                        var preferredFormat = frameSource.SupportedFormats
                            .OrderByDescending(f => f.VideoFormat.Width * f.VideoFormat.Height)
                            .FirstOrDefault(f => f.VideoFormat.Width <= 1920 && f.VideoFormat.Height <= 1080);

                        if (preferredFormat != null &&
                            (frameSource.CurrentFormat == null || frameSource.CurrentFormat.VideoFormat.Width == 0))
                        {
                            try { await frameSource.SetFormatAsync(preferredFormat); } catch { }
                        }

                        string resText = "Automática";
                        var currentFormat = frameSource.CurrentFormat;
                        if (currentFormat?.VideoFormat != null && currentFormat.VideoFormat.Width > 0)
                        {
                            uint w = currentFormat.VideoFormat.Width;
                            uint h = currentFormat.VideoFormat.Height;
                            uint fps = currentFormat.FrameRate.Denominator > 0
                                ? currentFormat.FrameRate.Numerator / currentFormat.FrameRate.Denominator
                                : 30;
                            resText = $"{w} x {h} ({fps} fps)";
                        }

                        // Prepara o container visual antes de iniciar o stream
                        LayoutPlaceholder.IsVisible = false;
                        CameraContainer.IsVisible = true;

                        // Cria Grid WinUI nativo que realiza Measure/Arrange real nos elementos filhos
                        _nativeHostGrid = new Microsoft.UI.Xaml.Controls.Grid
                        {
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
                        };

                        // 1. MediaPlayerElement vinculado ao MediaSource da câmera
                        _mediaPlayerElement = new MediaPlayerElement
                        {
                            AreTransportControlsEnabled = false,
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
                        };

                        _mediaPlayer = new MediaPlayer
                        {
                            AutoPlay = true,
                            RealTimePlayback = true,
                            Source = MediaSource.CreateFromMediaFrameSource(frameSource)
                        };
                        _mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

                        // 2. Superfície Image alimentada por MediaFrameReader + SoftwareBitmapSource (garante renderização mesmo sem SwapChain D3D)
                        _bitmapSource = new SoftwareBitmapSource();
                        _previewImage = new Microsoft.UI.Xaml.Controls.Image
                        {
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
                            Source = _bitmapSource
                        };

                        _nativeHostGrid.Children.Add(_mediaPlayerElement);
                        _nativeHostGrid.Children.Add(_previewImage);

                        if (CameraContainer.Handler?.PlatformView is ContentPanel panel)
                        {
                            AttachNativeHostToPanel(panel);
                        }

                        // Inicia MediaFrameReader em tempo real para alimentar SoftwareBitmapSource na UI Thread
                        try
                        {
                            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8);
                        }
                        catch
                        {
                            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource);
                        }

                        _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                        _frameReader.FrameArrived += OnFrameArrived;
                        await _frameReader.StartAsync();

                        // Inicia o playback no MediaPlayer via DispatcherQueue
                        _mediaPlayer.Play();

                        if (_attachedPanel != null)
                        {
                            SyncNativeHostLayout(_attachedPanel);
                        }

                        LblPermission.Text = "Ativa (WinUI 3)";
                        LblPermission.TextColor = Color.FromArgb("#22C55E");
                        LblResolution.Text = resText;
                        BtnStopCamera.IsEnabled = true;

                        tcs.TrySetResult(true);
                    }
                    catch (Exception uiEx)
                    {
                        tcs.TrySetException(uiEx);
                    }
                }

                if (dispatcherQueue != null)
                {
                    if (dispatcherQueue.HasThreadAccess)
                    {
                        await RunOnDispatcherAsync();
                    }
                    else
                    {
                        dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            await RunOnDispatcherAsync();
                        });
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(RunOnDispatcherAsync);
                }

                await tcs.Task;
#else
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        _camView = new CameraView
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill
                        };

                        CameraContainer.Content = _camView;
                        LayoutPlaceholder.IsVisible = false;
                        CameraContainer.IsVisible = true;

                        await Task.Delay(200);

                        var cameras = await _camView.GetAvailableCameras(_cts.Token);
                        if (cameras != null && cameras.Count > 0)
                        {
                            _camView.SelectedCamera = cameras.FirstOrDefault();
                            LblDevice.Text = _camView.SelectedCamera?.Name ?? "Câmera Padrão";
                            await _camView.StartCameraPreview(_cts.Token);
                            BtnStopCamera.IsEnabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Erro na Câmera", ex.ToString(), "OK");
                    }
                });
#endif
            }
            catch (Exception ex)
            {
                StopCamera();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    LblPermission.Text = "Erro de Dispositivo";
                    LblPermission.TextColor = Color.FromArgb("#EF4444");
                    BtnStartCamera.IsEnabled = true;
                    await DisplayAlert("Erro na Câmera", $"Não foi possível iniciar a câmera:\n{ex}", "OK");
                });
            }
            finally
            {
                _cameraLock.Release();
            }
        }

#if WINDOWS
        private void AttachNativeHostToPanel(ContentPanel panel)
        {
            DetachPanelEvents();
            _attachedPanel = panel;

            // Preserva o primeiro filho MAUI (BoxView transparente de ancoragem) e remove hosts nativos anteriores
            while (panel.Children.Count > 1)
            {
                panel.Children.RemoveAt(panel.Children.Count - 1);
            }

            if (_nativeHostGrid != null)
            {
                panel.Children.Add(_nativeHostGrid);
            }

            panel.SizeChanged += OnPanelSizeChanged;
            panel.LayoutUpdated += OnPanelLayoutUpdated;
            SyncNativeHostLayout(panel);
        }

        private void DetachPanelEvents()
        {
            if (_attachedPanel != null)
            {
                _attachedPanel.SizeChanged -= OnPanelSizeChanged;
                _attachedPanel.LayoutUpdated -= OnPanelLayoutUpdated;
                while (_attachedPanel.Children.Count > 1)
                {
                    _attachedPanel.Children.RemoveAt(_attachedPanel.Children.Count - 1);
                }
                _attachedPanel = null;
            }
        }

        private void OnPanelSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (sender is ContentPanel panel)
            {
                SyncNativeHostLayout(panel);
            }
        }

        private void OnPanelLayoutUpdated(object? sender, object e)
        {
            if (_attachedPanel != null)
            {
                SyncNativeHostLayout(_attachedPanel);
            }
        }

        /// <summary>
        /// O ContentPanel do .NET MAUI não invoca Measure/Arrange em filhos WinUI nativos adicionados diretamente em panel.Children.
        /// Esta rotina força o Measure e Arrange do Grid nativo com as dimensões reais do container.
        /// </summary>
        private void SyncNativeHostLayout(ContentPanel panel)
        {
            if (_nativeHostGrid == null) return;

            double w = panel.ActualWidth > 0 ? panel.ActualWidth : CameraContainer.Width;
            double h = panel.ActualHeight > 0 ? panel.ActualHeight : CameraContainer.Height;

            if (w <= 0 || double.IsNaN(w)) w = 640;
            if (h <= 0 || double.IsNaN(h)) h = 480;

            if (Math.Abs(_nativeHostGrid.Width - w) > 0.5 || Math.Abs(_nativeHostGrid.Height - h) > 0.5 || _nativeHostGrid.ActualWidth == 0)
            {
                _nativeHostGrid.Width = w;
                _nativeHostGrid.Height = h;
                _nativeHostGrid.Measure(new Windows.Foundation.Size(w, h));
                _nativeHostGrid.Arrange(new Windows.Foundation.Rect(0, 0, w, h));
            }
        }

        private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            using var frame = sender.TryAcquireLatestFrame();
            var videoFrame = frame?.VideoMediaFrame;
            if (videoFrame == null) return;

            SoftwareBitmap? bitmap = videoFrame.SoftwareBitmap;
            bool ownsBitmap = false;

            if (bitmap == null) return;

            try
            {
                // SoftwareBitmapSource no WinUI 3 exige formato Bgra8 com alfa Premultiplied
                if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    ownsBitmap = true;
                }
                else
                {
                    bitmap = SoftwareBitmap.Copy(bitmap);
                    ownsBitmap = true;
                }

                bitmap = Interlocked.Exchange(ref _backBuffer, bitmap);
                bitmap?.Dispose();
                ownsBitmap = false;

                var nativeWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                var dq = nativeWindow?.DispatcherQueue;
                if (dq == null) return;

                dq.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    if (Interlocked.CompareExchange(ref _isRenderingFrame, 1, 0) != 0)
                        return;

                    try
                    {
                        SoftwareBitmap? latestBitmap;
                        while ((latestBitmap = Interlocked.Exchange(ref _backBuffer, null)) != null)
                        {
                            using (latestBitmap)
                            {
                                if (_bitmapSource != null)
                                {
                                    await _bitmapSource.SetBitmapAsync(latestBitmap);
                                }
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        Interlocked.Exchange(ref _isRenderingFrame, 0);
                    }
                });
            }
            catch
            {
                if (ownsBitmap)
                {
                    bitmap?.Dispose();
                }
            }
        }

        private void StartDeviceWatcher()
        {
            if (_deviceWatcher != null) return;

            try
            {
                _deviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
                _deviceWatcher.Added += OnVideoDeviceAdded;
                _deviceWatcher.Removed += OnVideoDeviceRemoved;
                _deviceWatcher.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao iniciar DeviceWatcher de vídeo: {ex}");
            }
        }

        private void StopDeviceWatcher()
        {
            if (_deviceWatcher == null) return;

            try
            {
                _deviceWatcher.Added -= OnVideoDeviceAdded;
                _deviceWatcher.Removed -= OnVideoDeviceRemoved;
                if (_deviceWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                {
                    _deviceWatcher.Stop();
                }
            }
            catch { }
            finally
            {
                _deviceWatcher = null;
            }
        }

        private void OnVideoDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            if (_userRequestedStart && _mediaCapture == null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(400);
                    await InitializeCameraAsync(args.Id);
                });
            }
        }

        private void OnVideoDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (_activeDeviceId != null && args.Id == _activeDeviceId)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await CleanupNativeCameraResourcesAsync();
                    LblDevice.Text = "Reconectando USB...";
                    LblPermission.Text = "Aguardando USB";
                    LblPermission.TextColor = Color.FromArgb("#EAB308");

                    var remaining = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                    if (_userRequestedStart && remaining != null && remaining.Count > 0)
                    {
                        await InitializeCameraAsync(remaining[0].Id);
                    }
                    else
                    {
                        CameraContainer.IsVisible = false;
                        LayoutPlaceholder.IsVisible = true;
                        LblPlaceholderTitle.Text = "Webcam desconectada";
                        LblPlaceholderDesc.Text = "A câmera foi removida da porta USB. Reconecte para retomar automaticamente.";
                        BtnStartCamera.IsEnabled = true;
                        BtnStopCamera.IsEnabled = false;
                    }
                });
            }
        }

        private void OnMediaCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine($"MediaCapture falhou ({errorEventArgs.Code}): {errorEventArgs.Message}");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await CleanupNativeCameraResourcesAsync();
                if (_userRequestedStart)
                {
                    await Task.Delay(500);
                    var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                    if (devices != null && devices.Count > 0)
                    {
                        await InitializeCameraAsync(devices[0].Id);
                        return;
                    }
                }

                CameraContainer.IsVisible = false;
                LayoutPlaceholder.IsVisible = true;
                LblPermission.Text = "Desconectada";
                LblPermission.TextColor = Color.FromArgb("#EAB308");
                BtnStartCamera.IsEnabled = true;
                BtnStopCamera.IsEnabled = false;
            });
        }

        private async Task CleanupNativeCameraResourcesAsync()
        {
            try
            {
                DetachPanelEvents();

                if (_frameReader != null)
                {
                    try
                    {
                        _frameReader.FrameArrived -= OnFrameArrived;
                        await _frameReader.StopAsync();
                    }
                    catch { }
                    try { _frameReader.Dispose(); } catch { }
                    _frameReader = null;
                }

                var leftover = Interlocked.Exchange(ref _backBuffer, null);
                leftover?.Dispose();

                if (_previewImage != null)
                {
                    try { _previewImage.Source = null; } catch { }
                    _previewImage = null;
                }

                if (_bitmapSource != null)
                {
                    try { _bitmapSource.Dispose(); } catch { }
                    _bitmapSource = null;
                }

                if (_mediaPlayerElement != null)
                {
                    try { _mediaPlayerElement.SetMediaPlayer(null); } catch { }
                    _mediaPlayerElement = null;
                }

                if (_mediaPlayer != null)
                {
                    try { _mediaPlayer.Pause(); } catch { }
                    try { _mediaPlayer.Source = null; } catch { }
                    try { _mediaPlayer.Dispose(); } catch { }
                    _mediaPlayer = null;
                }

                if (_nativeHostGrid != null)
                {
                    try { _nativeHostGrid.Children.Clear(); } catch { }
                    _nativeHostGrid = null;
                }

                if (_mediaCapture != null)
                {
                    try { _mediaCapture.Failed -= OnMediaCaptureFailed; } catch { }
                    try { _mediaCapture.Dispose(); } catch { }
                    _mediaCapture = null;
                }
            }
            catch { }
        }
#endif

        private void OnStopCameraClicked(object sender, EventArgs e)
        {
            _userRequestedStart = false;
            StopCamera();
        }

        private void StopCamera()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
#if WINDOWS
                    await CleanupNativeCameraResourcesAsync();
#else
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;

                    if (_camView != null)
                    {
                        _camView.StopCameraPreview();
                        _camView.SelectedCamera = null;
                        _camView = null;
                    }
#endif
                    CameraContainer.IsVisible = false;
                    LayoutPlaceholder.IsVisible = true;

                    BtnStartCamera.IsEnabled = true;
                    BtnStopCamera.IsEnabled = false;

                    LblDevice.Text = "Desconectado";
                    LblResolution.Text = "N/A";
                }
                catch { }
            });
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            _userRequestedStart = false;
            StopCamera();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
