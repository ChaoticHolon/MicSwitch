using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MicSwitch.Hotkeys;

namespace MicSwitch.Controls;

/// <summary>
/// Captures a hotkey: click, then press a key combination or a middle/side mouse button (or scroll).
/// Backspace, Delete or Escape clears it. A lone modifier (e.g. Left Ctrl) is accepted when released.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture), typeof(HotkeyGesture), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(HotkeyGesture.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).UpdateText()));

    private Key pendingModifierKey = Key.None;

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        ContextMenu = null;
        Cursor = Cursors.Hand;
        MinWidth = 150;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        ToolTip = "Click, then press a key, key combination or mouse button. Backspace clears.";
        // Use the theme's TextBox look (implicit styles don't apply to derived types).
        SetResourceReference(StyleProperty, typeof(TextBox));
        UpdateText();
    }

    /// <summary>Raised when any HotkeyBox starts or stops capturing, so global hotkeys can be paused meanwhile.</summary>
    public static event EventHandler<bool>? CapturingChanged;

    public HotkeyGesture Gesture
    {
        get => (HotkeyGesture)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        CapturingChanged?.Invoke(this, true);
        UpdateText();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        pendingModifierKey = Key.None;
        CapturingChanged?.Invoke(this, false);
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var modifiers = CurrentModifiers(key);

        if (modifiers == HotkeyModifiers.None && key is Key.Escape or Key.Back or Key.Delete)
        {
            Commit(HotkeyGesture.Empty);
            return;
        }

        if (key is Key.Tab && modifiers == HotkeyModifiers.None)
        {
            e.Handled = false;
            return;
        }

        if (IsModifier(key))
        {
            pendingModifierKey = key;
            Text = $"{Format(modifiers)}{key}…";
            return;
        }

        Commit(new HotkeyGesture(modifiers, key.ToString()));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (pendingModifierKey != Key.None && key == pendingModifierKey)
        {
            Commit(new HotkeyGesture(CurrentModifiers(key), key.ToString()));
        }

        e.Handled = true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        var button = e.ChangedButton switch
        {
            MouseButton.Middle => MouseHotkey.Middle,
            MouseButton.XButton1 => MouseHotkey.XButton1,
            MouseButton.XButton2 => MouseHotkey.XButton2,
            _ => MouseHotkey.None,
        };

        if (IsKeyboardFocused && button != MouseHotkey.None)
        {
            e.Handled = true;
            Commit(new HotkeyGesture(CurrentModifiers(Key.None), Mouse: button));
            return;
        }

        base.OnPreviewMouseDown(e);
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (!IsKeyboardFocused)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        e.Handled = true;
        Commit(new HotkeyGesture(CurrentModifiers(Key.None), Mouse: e.Delta > 0 ? MouseHotkey.WheelUp : MouseHotkey.WheelDown));
    }

    private void Commit(HotkeyGesture gesture)
    {
        pendingModifierKey = Key.None;
        Gesture = gesture;
        UpdateText();
    }

    private void UpdateText() => Text = Gesture.IsEmpty
        ? IsKeyboardFocused ? "Press a key…" : "Not set"
        : Gesture.ToString();

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static HotkeyModifiers CurrentModifiers(Key exclude)
    {
        var result = HotkeyModifiers.None;
        if (Held(Key.LeftCtrl, Key.RightCtrl)) result |= HotkeyModifiers.Control;
        if (Held(Key.LeftAlt, Key.RightAlt)) result |= HotkeyModifiers.Alt;
        if (Held(Key.LeftShift, Key.RightShift)) result |= HotkeyModifiers.Shift;
        if (Held(Key.LWin, Key.RWin)) result |= HotkeyModifiers.Windows;
        return result;

        bool Held(Key left, Key right) => exclude != left && exclude != right && (Keyboard.IsKeyDown(left) || Keyboard.IsKeyDown(right));
    }

    private static string Format(HotkeyModifiers modifiers) =>
        modifiers == HotkeyModifiers.None ? string.Empty : new HotkeyGesture(modifiers, "x").ToString()[..^1];
}
