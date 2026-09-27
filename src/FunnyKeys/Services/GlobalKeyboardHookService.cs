using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace FunnyKeys.Services;
public sealed class GlobalKeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly HookProc _callback;
    private readonly KeyPressPolicy _policy = new();
    private IntPtr _hook;
    private uint _threadId;
    private Exception? _startupError;
    private bool _disposed;
    public volatile bool Enabled;
    public volatile bool Repeat;
    public volatile bool IncludeModifiers;
    public event Action? Pressed;
    public event Action? ToggleRequested;
    public bool HotkeyAvailable { get; private set; }
    public string Diagnostics => _policy.Diagnostics;
    public bool LastPressWasRepeat => _policy.LastWasRepeat;
    public GlobalKeyboardHookService()
    {
        _callback = Callback;
        _thread = new Thread(Run) { IsBackground = true, Name = "FunnyKeys keyboard listener" };
        _thread.Start(); _ready.Wait();
        if (_startupError != null) throw _startupError;
    }
    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // Create the thread message queue before signaling readiness.
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) _startupError = new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not enable the keyboard listener.");
        HotkeyAvailable = _hook != IntPtr.Zero && RegisterHotKey(IntPtr.Zero, 1, 0x4000 | 0x2 | 0x4, 0x7B);
        _ready.Set();
        if (_hook == IntPtr.Zero) return;
        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message == 0x0312) ToggleRequested?.Invoke();
                TranslateMessage(ref message); DispatchMessage(ref message);
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, 1);
            UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; _policy.Clear();
        }
    }
    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        // Never do disk IO, decode audio, wait on UI, or suppress input in this callback.
        try
        {
            if (code >= 0)
            {
                var key = Marshal.PtrToStructure<KbdData>(data);
                bool down = message == (IntPtr)0x100 || message == (IntPtr)0x104;
                bool up = message == (IntPtr)0x101 || message == (IntPtr)0x105;
                if ((down || up) && _policy.Process((int)key.vkCode, down, Repeat, IncludeModifiers) && Enabled)
                {
                    // The toggle chord itself is silent.
                    if (!(key.vkCode == 0x7B && (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x10) & 0x8000) != 0))
                        Pressed?.Invoke();
                }
            }
        }
        catch { /* A managed listener failure must never affect the input chain. */ }
        return CallNextHookEx(_hook, code, message, data);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; Enabled = false;
        if (_thread.IsAlive) { PostThreadMessage(_threadId, 0x12, IntPtr.Zero, IntPtr.Zero); _thread.Join(); }
        _ready.Dispose(); GC.KeepAlive(_callback);
    }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct KbdData { public uint vkCode, scanCode, flags, time; public UIntPtr extraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x, y; public uint privateData; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
