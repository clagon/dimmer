using Dimmer.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;

namespace Dimmer.SettingsUi;

/// <summary>
/// Shortcut の入力欄。入力欄にフォーカスを当ててキーを押すと、その組み合わせを記録する（記録中は既存の Shortcut をすべて止める）。
/// 物理キーボードに無いキー（F13〜F24 など）のために、修飾キーのチェックボックスとキーのドロップダウンでも選べる。
/// どちらの方法でも、修飾キーが必須であること（F13〜F24 は例外）と、アプリ内で重複しないことを確かめる。
/// </summary>
public sealed class ShortcutEditor : StackPanel
{
    private static readonly VirtualKey[] ModifierKeys =
    [
        VirtualKey.Control, VirtualKey.LeftControl, VirtualKey.RightControl, VirtualKey.Menu, VirtualKey.LeftMenu, VirtualKey.RightMenu,
        VirtualKey.Shift, VirtualKey.LeftShift, VirtualKey.RightShift, VirtualKey.LeftWindows, VirtualKey.RightWindows,
    ];

    private readonly App _app;
    private readonly object _target;
    private readonly Func<string?> _get;
    private readonly Action<string?> _set;
    private readonly TextBox _box;
    private readonly CheckBox _ctrl = new() { Content = "Ctrl", MinWidth = 0 };
    private readonly CheckBox _alt = new() { Content = "Alt", MinWidth = 0 };
    private readonly CheckBox _shift = new() { Content = "Shift", MinWidth = 0 };
    private readonly CheckBox _win = new() { Content = "Win", MinWidth = 0 };
    private readonly ComboBox _key = new() { PlaceholderText = Text.Get("UiKey"), Width = 150 };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    public ShortcutEditor(App app, object target, Func<string?> get, Action<string?> set)
    {
        _app = app;
        _target = target;
        _get = get;
        _set = set;
        Spacing = 6;

        Children.Add(new TextBlock { Text = Text.Get("UiShortcut") });
        _box = new TextBox { IsReadOnly = true, Width = 260, PlaceholderText = Text.Get("UiShortcutPlaceholder") };
        _box.GotFocus += (_, _) => { _app.SuspendShortcuts(); _box.PlaceholderText = Text.Get("UiShortcutRecording"); };
        _box.LostFocus += (_, _) => { _box.PlaceholderText = Text.Get("UiShortcutPlaceholder"); _app.ResumeShortcuts(); ShowConflict(); };
        _box.PreviewKeyDown += OnKeyDown;

        var clear = new Button { Content = Text.Get("UiClear") };
        clear.Click += (_, _) => { _set(null); _app.SettingsChanged(); Sync(); Show(null); };
        Children.Add(SettingsWindow.Row(_box, clear));

        foreach (var name in Keys.Names) _key.Items.Add(name);
        var assign = new Button { Content = Text.Get("UiAssign") };
        assign.Click += (_, _) =>
        {
            if (_key.SelectedItem is not string name || Keys.Code(name) is not { } code) return;
            var modifiers = (_ctrl.IsChecked == true ? Modifiers.Ctrl : 0) | (_alt.IsChecked == true ? Modifiers.Alt : 0)
                | (_shift.IsChecked == true ? Modifiers.Shift : 0) | (_win.IsChecked == true ? Modifiers.Win : 0);
            TryAssign(new Shortcut(modifiers, code));
        };
        Children.Add(SettingsWindow.Row(_ctrl, _alt, _shift, _win, _key, assign));
        Children.Add(_message);

        Sync();
        ShowConflict();
    }

    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        e.Handled = true;
        // Tab はフォーカスの移動に使えるよう記録しない
        if (e.Key == VirtualKey.Tab) { e.Handled = false; return; }
        if (ModifierKeys.Contains(e.Key)) return;

        static bool Down(VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);
        var modifiers = (Down(VirtualKey.Control) ? Modifiers.Ctrl : 0) | (Down(VirtualKey.Menu) ? Modifiers.Alt : 0)
            | (Down(VirtualKey.Shift) ? Modifiers.Shift : 0) | (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows) ? Modifiers.Win : 0);
        TryAssign(new Shortcut(modifiers, (int)e.Key));
    }

    private void TryAssign(Shortcut shortcut)
    {
        if (!shortcut.IsValid) { Show(Text.Get("ShortcutInvalid")); return; }
        if (_app.ShortcutOwner(shortcut, _target) is { } owner) { Show(Text.Format("UiShortcutInUse", owner)); return; }

        _set(shortcut.ToString());
        _app.SettingsChanged();
        Sync();
        Show(null);
        // 記録中（フォーカスがある）なら、フォーカスが外れて登録し直したときに衝突を確かめる
        if (_box.FocusState == FocusState.Unfocused) ShowConflict();
    }

    /// <summary>入力欄・チェックボックス・ドロップダウンを今の Shortcut に合わせる。</summary>
    private void Sync()
    {
        var s = Shortcut.Parse(_get());
        _box.Text = s?.ToString() ?? "";
        _ctrl.IsChecked = s?.Modifiers.HasFlag(Modifiers.Ctrl) ?? false;
        _alt.IsChecked = s?.Modifiers.HasFlag(Modifiers.Alt) ?? false;
        _shift.IsChecked = s?.Modifiers.HasFlag(Modifiers.Shift) ?? false;
        _win.IsChecked = s?.Modifiers.HasFlag(Modifiers.Win) ?? false;
        _key.SelectedItem = s is null ? null : Keys.Name(s.Key);
    }

    /// <summary>登録に失敗している（他のアプリとの衝突など）なら警告を出す。</summary>
    private void ShowConflict()
    {
        var current = Shortcut.Parse(_get());
        var failed = current is null ? null : _app.Shortcuts.Failed.FirstOrDefault(f => Shortcut.Parse(f.Shortcut) == current);
        Show(failed is null ? null : Text.Format("UiShortcutFailed", failed.Reason));
    }

    private void Show(string? message)
    {
        _message.Text = message ?? "";
        _message.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        _message.Foreground = SettingsWindow.Res<Microsoft.UI.Xaml.Media.Brush>("SystemFillColorCautionBrush");
    }
}
