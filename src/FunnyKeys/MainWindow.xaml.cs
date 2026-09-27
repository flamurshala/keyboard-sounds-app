using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FunnyKeys.ViewModels;

namespace FunnyKeys;
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _hintShown;
    public bool IsExiting { get; set; }
    public Action? TrayHint { get; set; }
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); _viewModel = viewModel; DataContext = viewModel;
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "FunnyKeys.ico");
        if (File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath));
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
    }
    public void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray) HideToTray();
    }
    private void HideToTray() { Hide(); if (!_hintShown) { _hintShown = true; TrayHint?.Invoke(); } }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsExiting && _viewModel.MinimizeToTray) { e.Cancel = true; HideToTray(); }
        else if (!IsExiting) { IsExiting = true; Application.Current.Shutdown(); }
        base.OnClosing(e);
    }
}
