using System;
using Microsoft.Win32;

namespace FunnyKeys.Services;
public static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue("FunnyKeys") is string;
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("FunnyKeys", $"\"{Environment.ProcessPath}\" --startup");
        else key.DeleteValue("FunnyKeys", false);
    }
}
