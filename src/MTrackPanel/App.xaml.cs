using System.Windows;
using MTrackPanel.Services;

namespace MTrackPanel;

public partial class App : Application
{
    private const string MutexName = @"Local\MAudio.MTrackPanel.SingleInstance";

    private Mutex? _mutex;
    private ActivationWindow? _activation;
    private TrayIcon? _tray;
    private MainWindow? _main;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The autostart entry passes "0" (like the original MAPanel.exe); start hidden in the tray then.
        bool startHidden = e.Args.Any(a => a is "0" or "--tray" or "/tray");

        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is running: bring its window up the same way the ASIO driver does.
            if (!startHidden) ActivationWindow.ActivateExistingInstance();
            Shutdown();
            return;
        }

        _main = new MainWindow();
        _activation = new ActivationWindow();
        _activation.ActivationRequested += ShowMainWindow;
        _tray = new TrayIcon(ShowMainWindow, ExitApp);

        if (!startHidden) ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_main is null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        _main.Topmost = true;  // reliably bring to front even when launched from another app
        _main.Topmost = false;
        _main.Focus();
    }

    public void ExitApp()
    {
        IsExiting = true;
        _main?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _activation?.Dispose();
        _main?.ViewModel.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
