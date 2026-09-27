using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FunnyKeys.ViewModels;

namespace FunnyKeys.Services;
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _image;
    public TrayIconService(MainViewModel viewModel, Action open)
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "FunnyKeys.ico");
        _image = File.Exists(iconPath) ? new Icon(iconPath) : (Icon)SystemIcons.Application.Clone();
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open FunnyKeys", null, (_, _) => open());
        _menu.Items.Add(new ToolStripSeparator());
        var enable = _menu.Items.Add("Enable Keyboard Sounds", null, (_, _) => viewModel.Enabled = true);
        var disable = _menu.Items.Add("Disable Keyboard Sounds", null, (_, _) => viewModel.Enabled = false);
        var sounds = new ToolStripMenuItem("Current Sound"); _menu.Items.Add(sounds);
        _menu.Opening += (_, _) =>
        {
            enable.Enabled = !viewModel.Enabled; disable.Enabled = viewModel.Enabled;
            while (sounds.DropDownItems.Count > 0) { var old = sounds.DropDownItems[0]; sounds.DropDownItems.RemoveAt(0); old.Dispose(); }
            foreach (var sound in viewModel.Sounds)
            {
                var item = new ToolStripMenuItem(sound.Name) { Checked = viewModel.SelectedSound == sound };
                item.Click += (_, _) => viewModel.SelectedSound = sound; sounds.DropDownItems.Add(item);
            }
        };
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit FunnyKeys", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _icon = new NotifyIcon { Text = "FunnyKeys", Icon = _image, Visible = true, ContextMenuStrip = _menu };
        _icon.DoubleClick += (_, _) => open();
        viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(viewModel.Enabled)) _icon.Text = "FunnyKeys · " + viewModel.Status; };
        viewModel.HotkeyToggled += () => Notify(viewModel.Enabled ? "FunnyKeys Enabled" : "FunnyKeys Disabled");
    }
    public void Notify(string text) => _icon.ShowBalloonTip(2000, "FunnyKeys", text, ToolTipIcon.Info);
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _image.Dispose(); }
}
