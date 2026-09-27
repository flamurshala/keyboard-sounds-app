using System.Collections.Generic;
using System.Threading;

namespace FunnyKeys.Services;
// State is transient: only which physical keys are currently held, never characters or history.
public sealed class KeyPressPolicy
{
    private readonly HashSet<int> _held = new();
    private long _newPresses, _repeatedDowns, _triggers;
    public bool LastWasRepeat { get; private set; }
    public string Diagnostics => $"Physical downs: {Interlocked.Read(ref _newPresses)} | Repeat downs: {Interlocked.Read(ref _repeatedDowns)} | Allowed triggers: {Interlocked.Read(ref _triggers)}";
    public bool Process(int key, bool down, bool repeat, bool modifiers)
    {
        if (!down) { _held.Remove(key); return false; }
        bool first = _held.Add(key);
        LastWasRepeat = !first;
        if (first) Interlocked.Increment(ref _newPresses); else Interlocked.Increment(ref _repeatedDowns);
        if (!modifiers && IsModifier(key)) return false;
        bool accepted = repeat || first;
        if (accepted) Interlocked.Increment(ref _triggers);
        return accepted;
    }
    public void Clear() => _held.Clear();
    public static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
}
