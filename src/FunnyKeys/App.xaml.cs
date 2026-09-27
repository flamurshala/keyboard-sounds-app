using System;
using System.Threading;
using System.Windows;
using FunnyKeys.Services;
using FunnyKeys.ViewModels;

namespace FunnyKeys;
public partial class App : Application
{
    private Mutex? _mutex;
    private MainViewModel? _viewModel;
    private TrayIconService? _tray;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FunnyKeys.Show");
        _mutex = new Mutex(true, "Local\\FunnyKeys.Desktop", out bool first);
        if (!first) { _showEvent.Set(); Shutdown(); return; }
        try
        {
            _viewModel = new MainViewModel();
            var window = new MainWindow(_viewModel); MainWindow = window;
            _tray = new TrayIconService(_viewModel, window.Restore);
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(window.Restore), null, Timeout.Infinite, false);
            window.TrayHint = () => _tray.Notify("Still running in the tray. Double-click the icon to open; choose Exit FunnyKeys to quit.");
            if (!_viewModel.StartMinimized) window.Show();
        }
        catch (Exception ex) { MessageBox.Show("FunnyKeys could not start: " + ex.Message, "FunnyKeys", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow is MainWindow window) window.IsExiting = true;
        _showWait?.Unregister(null); _showEvent?.Dispose();
        _tray?.Dispose(); _viewModel?.Dispose(); _mutex?.Dispose(); base.OnExit(e);
    }
}
