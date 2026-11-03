using Dimmer.Core;
using Dimmer.Monitors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Monitor = Dimmer.Monitors.Monitor;

namespace Dimmer.SettingsUi;

/// <summary>
/// Monitor Name・Profile・Profile Cycle・Adjustment・Shortcut・全般設定を編集する画面。
/// 変更したらすぐに保存する（不完全な状態もそのまま保存し、「無効」と表示する）。
/// 外部からの設定ファイルの書き換えは監視しない。
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly App _app;
    private readonly NavigationView _nav = new()
    {
        IsSettingsVisible = false,
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
        IsPaneToggleButtonVisible = false,
        OpenPaneLength = 200,
    };
    private readonly HashSet<object> _expanded = [];
    private string _page = "Monitors";

    private AppSettings S => _app.Settings.Settings;

    public SettingsWindow(App app)
    {
        _app = app;
        Title = Text.Get("SettingsTitle");
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1040, 780));

        foreach (var (tag, glyph) in new[] { ("Monitors", ""), ("Profiles", ""), ("Cycles", ""), ("Adjustments", ""), ("General", "") })
            _nav.MenuItems.Add(new NavigationViewItem { Content = Text.Get($"Nav{tag}"), Tag = tag, Icon = new FontIcon { Glyph = glyph } });
        _nav.SelectionChanged += (_, e) =>
        {
            if (e.SelectedItem is NavigationViewItem { Tag: string tag }) { _page = tag; Render(); }
        };
        _nav.SelectedItem = _nav.MenuItems[0];
        Content = _nav;

        _app.Monitors.Changed += OnMonitorsChanged;
        Closed += (_, _) => _app.Monitors.Changed -= OnMonitorsChanged;
    }

    // 列挙の完了・抜き差し・名前の変更で一覧が変わったら、Monitor を選ぶ欄があるページを描き直す
    private void OnMonitorsChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        if (_page is "Monitors" or "Profiles" or "Adjustments") Render();
    });

    private void Changed() => _app.SettingsChanged();

    private void Render()
    {
        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(36, 28, 36, 36), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = Text.Get($"Nav{_page}"), Style = Res<Style>("TitleTextBlockStyle"), Margin = new Thickness(0, 0, 0, 8) });
        switch (_page)
        {
            case "Monitors": RenderMonitors(panel); break;
            case "Profiles": RenderProfiles(panel); break;
            case "Cycles": RenderCycles(panel); break;
            case "Adjustments": RenderAdjustments(panel); break;
            case "General": RenderGeneral(panel); break;
        }
        _nav.Content = new ScrollViewer { Content = panel };
    }

    // ---- Monitor ----

    private void RenderMonitors(StackPanel panel)
    {
        var monitors = _app.Monitors.Monitors;
        if (monitors.Count == 0) panel.Children.Add(Note(Text.Get(_app.Monitors.Ready.IsCompleted ? "UiNoMonitors" : "UiLoadingMonitors")));

        foreach (var m in monitors)
        {
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock { Text = m.DisplayName, Style = Res<Style>("SubtitleTextBlockStyle") });
            body.Children.Add(Note($"{m.ModelName} · {m.Key}"));

            var name = new TextBox { Header = Text.Get("UiMonitorName"), PlaceholderText = m.ModelName, Text = S.MonitorNames.GetValueOrDefault(m.Key) ?? "", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
            name.LostFocus += (_, _) =>
            {
                var value = name.Text.Trim();
                if (value == (S.MonitorNames.GetValueOrDefault(m.Key) ?? "")) return;
                if (value.Length == 0) S.MonitorNames.Remove(m.Key);
                else S.MonitorNames[m.Key] = value;
                _app.SettingsChanged(monitorNamesChanged: true);
            };
            body.Children.Add(name);

            if (!m.IsControllable) body.Children.Add(Warning(Text.Get("UiMonitorNotControllable")));
            body.Children.Add(new TextBlock
            {
                Text = Text.Format("UiMonitorSettings", string.Join("、", m.Settings.Select(s => s.Value.Count == 0 || s.Key == Setting.Orientation
                    ? Text.Setting(s.Key)
                    : $"{Text.Setting(s.Key)}（{string.Join(" / ", s.Value.Select(v => Text.Value(s.Key, v)))}）"))),
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(Card(body));
        }
    }

    // ---- Profile ----

    private void RenderProfiles(StackPanel panel)
    {
        var create = new Button { Content = Text.Get("UiNew") };
        create.Click += (_, _) =>
        {
            var p = new Profile { Name = Text.Get("UiNewProfileName") };
            S.Profiles.Add(p);
            _expanded.Add(p);
            Changed();
            Render();
        };
        var capture = new Button { Content = Text.Get("UiCreateFromCurrent") };
        capture.Click += async (_, _) =>
        {
            capture.IsEnabled = false;
            capture.Content = Text.Get("UiReading");
            var p = new Profile { Name = Text.Get("UiNewProfileName"), Entries = await ReadCurrentAsync() };
            S.Profiles.Add(p);
            _expanded.Add(p);
            Changed();
            Render();
        };
        panel.Children.Add(Row(create, capture));
        panel.Children.Add(Note(Text.Get("UiProfilesOrderNote")));

        for (var i = 0; i < S.Profiles.Count; i++)
        {
            var profile = S.Profiles[i];
            var expander = NewExpander(profile, profile.Name);
            var body = new StackPanel { Spacing = 12 };

            var name = new TextBox { Header = Text.Get("UiName"), Text = profile.Name, Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
            name.TextChanged += (_, _) => { profile.Name = name.Text; expander.Header = name.Text; Changed(); };
            body.Children.Add(name);
            body.Children.Add(new ShortcutEditor(_app, profile, () => profile.Shortcut, v => profile.Shortcut = v));

            body.Children.Add(new TextBlock { Text = Text.Get("UiEntries"), Style = Res<Style>("BodyStrongTextBlockStyle") });
            for (var j = 0; j < profile.Entries.Count; j++) body.Children.Add(EntryRow(profile, j));
            body.Children.Add(AddEntryRow(profile));

            var apply = new Button { Content = Text.Get("UiApplyNow"), Style = Res<Style>("AccentButtonStyle") };
            apply.Click += async (_, _) => { apply.IsEnabled = false; await _app.ApplyProfileAsync(profile); apply.IsEnabled = true; };
            body.Children.Add(Row(apply, MoveButtons(S.Profiles, i), DeleteButton(() =>
            {
                S.Profiles.Remove(profile);
                // 削除した Profile は Cycle から自動で取り除く（SettingsChanged の中で行う）
                Changed();
            })));

            expander.Content = body;
            panel.Children.Add(expander);
        }
    }

    /// <summary>全 Monitor・全 Setting の現在値を読む。Profile に含めるのは明るさとコントラストだけにしておく。</summary>
    private async Task<List<ProfileEntry>> ReadCurrentAsync()
    {
        await _app.Monitors.Ready;
        using var lease = _app.Monitors.Lease();
        var monitors = _app.Monitors.Monitors;
        return await Task.Run(() =>
        {
            var entries = new List<ProfileEntry>();
            foreach (var m in monitors)
            {
                foreach (var setting in m.Settings.Keys.Order())
                {
                    try { entries.Add(new ProfileEntry(m.Key, setting, MonitorSettings.Read(m, setting), setting is Setting.Brightness or Setting.Contrast)); }
                    catch (SettingException e) { Log.Write($"Create from current: {e.Message}"); }
                }
            }
            return entries;
        });
    }

    private UIElement EntryRow(Profile profile, int index)
    {
        var entry = profile.Entries[index];
        var check = new CheckBox { IsChecked = entry.Enabled, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => { profile.Entries[index] = profile.Entries[index] with { Enabled = check.IsChecked == true }; Changed(); };

        var label = new TextBlock { Text = $"{MonitorLabel(entry.Monitor)} · {Text.Setting(entry.Setting)}", VerticalAlignment = VerticalAlignment.Center, Width = 300, TextTrimming = TextTrimming.CharacterEllipsis };
        var value = ValueEditor(entry.Monitor, entry.Setting, entry.Value, v => { profile.Entries[index] = profile.Entries[index] with { Value = v }; Changed(); });
        var delete = IconButton("", Text.Get("UiDelete"), () => { profile.Entries.RemoveAt(index); Changed(); Render(); });
        return Row(check, label, value, delete);
    }

    private UIElement AddEntryRow(Profile profile)
    {
        var monitors = _app.Monitors.Monitors;
        var monitor = new ComboBox { PlaceholderText = Text.Get("UiMonitor"), Width = 220 };
        foreach (var m in monitors) monitor.Items.Add(m.DisplayName);
        var setting = new ComboBox { PlaceholderText = Text.Get("UiSetting"), Width = 160 };
        var settings = new List<Setting>();
        monitor.SelectionChanged += (_, _) =>
        {
            setting.Items.Clear();
            settings.Clear();
            if (monitor.SelectedIndex < 0) return;
            foreach (var s in monitors[monitor.SelectedIndex].Settings.Keys.Order()) { settings.Add(s); setting.Items.Add(Text.Setting(s)); }
        };
        var add = new Button { Content = Text.Get("UiAdd") };
        add.Click += (_, _) =>
        {
            if (monitor.SelectedIndex < 0 || setting.SelectedIndex < 0) return;
            var m = monitors[monitor.SelectedIndex];
            var s = settings[setting.SelectedIndex];
            var choices = m.Settings[s];
            profile.Entries.Add(new ProfileEntry(m.Key, s, s.IsContinuous() ? 50 : choices.FirstOrDefault()));
            Changed();
            Render();
        };
        return Row(monitor, setting, add);
    }

    /// <summary>値の入力欄。連続値は 0〜100 の数値、選択式と Orientation は選択肢（Monitor がつながっていなければ数値）。</summary>
    private FrameworkElement ValueEditor(string monitorKey, Setting setting, int value, Action<int> set)
    {
        var choices = setting == Setting.Orientation
            ? SettingExtensions.Orientations
            : _app.Monitors.Monitors.FirstOrDefault(m => m.Key == monitorKey)?.Settings.GetValueOrDefault(setting);
        if (setting.IsContinuous() || choices is null || choices.Count == 0)
        {
            var box = new NumberBox { Value = value, Minimum = 0, Maximum = setting.IsContinuous() ? 100 : 255, Width = 180, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            box.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue)) set((int)Math.Round(e.NewValue)); };
            return box;
        }

        var values = choices.Contains(value) ? choices.ToList() : [.. choices, value];
        var combo = new ComboBox { Width = 180 };
        foreach (var v in values) combo.Items.Add(Text.Value(setting, v));
        combo.SelectedIndex = values.IndexOf(value);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0) set(values[combo.SelectedIndex]); };
        return combo;
    }

    // ---- Profile Cycle ----

    private void RenderCycles(StackPanel panel)
    {
        var create = new Button { Content = Text.Get("UiNew") };
        create.Click += (_, _) =>
        {
            var c = new ProfileCycle { Name = Text.Get("UiNewCycleName") };
            S.Cycles.Add(c);
            _expanded.Add(c);
            Changed();
            Render();
        };
        panel.Children.Add(create);

        foreach (var cycle in S.Cycles)
        {
            var problem = Cycles.Problem(cycle);
            var expander = NewExpander(cycle, CycleHeader(cycle));
            var body = new StackPanel { Spacing = 12 };
            if (problem != CycleProblem.None) body.Children.Add(Warning(Text.Format("UiInvalid", Text.Get($"CycleProblem_{problem}"))));

            var name = new TextBox { Header = Text.Get("UiName"), Text = cycle.Name, Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
            name.TextChanged += (_, _) => { cycle.Name = name.Text; expander.Header = CycleHeader(cycle); Changed(); };
            name.LostFocus += (_, _) => { if (Cycles.Problem(cycle) != problem) Render(); };
            body.Children.Add(name);
            body.Children.Add(new ShortcutEditor(_app, cycle, () => cycle.Shortcut, v => cycle.Shortcut = v));

            body.Children.Add(new TextBlock { Text = Text.Get("UiCycleProfiles"), Style = Res<Style>("BodyStrongTextBlockStyle") });
            for (var k = 0; k < cycle.ProfileIds.Count; k++)
            {
                var index = k;
                var combo = new ComboBox { Width = 260 };
                foreach (var p in S.Profiles) combo.Items.Add(p.Name);
                combo.SelectedIndex = S.Profiles.FindIndex(p => p.Id == cycle.ProfileIds[index]);
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedIndex < 0) return;
                    cycle.ProfileIds[index] = S.Profiles[combo.SelectedIndex].Id;
                    Changed();
                    Render();
                };
                body.Children.Add(Row(new TextBlock { Text = $"{index + 1}.", VerticalAlignment = VerticalAlignment.Center, Width = 24 }, combo,
                    MoveButtons(cycle.ProfileIds, index),
                    IconButton("", Text.Get("UiDelete"), () => { cycle.ProfileIds.RemoveAt(index); Changed(); Render(); })));
            }

            var add = new Button { Content = Text.Get("UiAddProfile"), IsEnabled = S.Profiles.Count > 0 };
            add.Click += (_, _) =>
            {
                // まだ並びに無い Profile を優先して足す
                var next = S.Profiles.FirstOrDefault(p => !cycle.ProfileIds.Contains(p.Id)) ?? S.Profiles[0];
                cycle.ProfileIds.Add(next.Id);
                Changed();
                Render();
            };
            body.Children.Add(Row(add, DeleteButton(() => { S.Cycles.Remove(cycle); Changed(); })));

            expander.Content = body;
            panel.Children.Add(expander);
        }
    }

    private static string CycleHeader(ProfileCycle c) =>
        Cycles.Problem(c) == CycleProblem.None ? c.Name : $"{c.Name}  —  {Text.Get("UiInvalidShort")}";

    // ---- Adjustment ----

    private void RenderAdjustments(StackPanel panel)
    {
        var create = new Button { Content = Text.Get("UiNew") };
        create.Click += (_, _) =>
        {
            var a = new Adjustment();
            S.Adjustments.Add(a);
            _expanded.Add(a);
            Changed();
            Render();
        };
        panel.Children.Add(create);

        Setting[] settings = [Setting.Brightness, Setting.Contrast, Setting.Volume];
        foreach (var adjustment in S.Adjustments)
        {
            var expander = NewExpander(adjustment, AdjustmentHeader(adjustment));
            var body = new StackPanel { Spacing = 12 };
            if (!adjustment.IsValid) body.Children.Add(Warning(Text.Format("UiInvalid", Text.Get("AdjustmentInvalid"))));

            var setting = new ComboBox { Header = Text.Get("UiSetting"), Width = 200 };
            foreach (var s in settings) setting.Items.Add(Text.Setting(s));
            setting.SelectedIndex = Array.IndexOf(settings, adjustment.Setting);
            setting.SelectionChanged += (_, _) =>
            {
                if (setting.SelectedIndex < 0) return;
                adjustment.Setting = settings[setting.SelectedIndex];
                Changed();
                Render();
            };

            // 対象: Cursor Monitor、全 Monitor、つながっている Monitor（つながっていない Monitor が対象なら、それも残す）
            var targets = new List<(string Key, string Label)> { (Adjustment.CursorMonitor, Text.Get("CursorMonitor")), (Adjustment.AllMonitors, Text.Get("AllMonitors")) };
            targets.AddRange(_app.Monitors.Monitors.Select(m => (m.Key, m.DisplayName)));
            if (!targets.Any(t => t.Key == adjustment.Target)) targets.Add((adjustment.Target, MonitorLabel(adjustment.Target)));
            var target = new ComboBox { Header = Text.Get("UiTarget"), Width = 260 };
            foreach (var t in targets) target.Items.Add(t.Label);
            target.SelectedIndex = targets.FindIndex(t => t.Key == adjustment.Target);
            target.SelectionChanged += (_, _) =>
            {
                if (target.SelectedIndex < 0) return;
                adjustment.Target = targets[target.SelectedIndex].Key;
                expander.Header = AdjustmentHeader(adjustment);
                Changed();
            };

            var delta = new NumberBox { Header = Text.Get("UiDelta"), Value = adjustment.Delta, Minimum = -100, Maximum = 100, Width = 160, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            delta.ValueChanged += (_, e) =>
            {
                if (double.IsNaN(e.NewValue)) return;
                var wasValid = adjustment.IsValid;
                adjustment.Delta = (int)Math.Round(e.NewValue);
                expander.Header = AdjustmentHeader(adjustment);
                Changed();
                if (wasValid != adjustment.IsValid) Render();
            };

            body.Children.Add(Row(setting, target, delta));
            body.Children.Add(new ShortcutEditor(_app, adjustment, () => adjustment.Shortcut, v => adjustment.Shortcut = v));
            body.Children.Add(DeleteButton(() => { S.Adjustments.Remove(adjustment); Changed(); }));

            expander.Content = body;
            panel.Children.Add(expander);
        }
    }

    private string AdjustmentHeader(Adjustment a) =>
        a.IsValid ? _app.AdjustmentName(a) : $"{_app.AdjustmentName(a)}  —  {Text.Get("UiInvalidShort")}";

    // ---- 全般 ----

    private void RenderGeneral(StackPanel panel)
    {
        var overlay = new ToggleSwitch { Header = Text.Get("UiOverlayEnabled"), IsOn = S.OverlayEnabled };
        overlay.Toggled += (_, _) => { S.OverlayEnabled = overlay.IsOn; Changed(); };
        panel.Children.Add(Card(overlay));
    }

    // ---- 部品 ----

    private string MonitorLabel(string key) =>
        _app.Monitors.Monitors.FirstOrDefault(m => m.Key == key)?.DisplayName
        ?? S.MonitorNames.GetValueOrDefault(key)
        ?? Text.Format("UiDisconnectedMonitor", key);

    private Expander NewExpander(object item, string header)
    {
        var e = new Expander
        {
            Header = header,
            IsExpanded = _expanded.Contains(item),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        e.Expanding += (_, _) => _expanded.Add(item);
        e.Collapsed += (_, _) => _expanded.Remove(item);
        return e;
    }

    private UIElement MoveButtons<T>(List<T> list, int index) => Row(
        IconButton("", Text.Get("UiMoveUp"), () => { if (index > 0) { (list[index - 1], list[index]) = (list[index], list[index - 1]); Changed(); Render(); } }, index > 0),
        IconButton("", Text.Get("UiMoveDown"), () => { if (index < list.Count - 1) { (list[index + 1], list[index]) = (list[index], list[index + 1]); Changed(); Render(); } }, index < list.Count - 1));

    private Button DeleteButton(Action delete)
    {
        var b = new Button { Content = Text.Get("UiDelete") };
        b.Click += (_, _) => { delete(); Render(); };
        return b;
    }

    private static Button IconButton(string glyph, string label, Action click, bool enabled = true)
    {
        var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 12 }, IsEnabled = enabled, Padding = new Thickness(8, 6, 8, 6) };
        ToolTipService.SetToolTip(b, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => click();
        return b;
    }

    internal static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var c in children) row.Children.Add(c);
        return row;
    }

    private static Border Card(UIElement child) => new()
    {
        Child = child,
        Padding = new Thickness(20, 16, 20, 16),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
        Background = Res<Brush>("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Res<Brush>("CardStrokeColorDefaultBrush"),
    };

    internal static TextBlock Note(string text) => new() { Text = text, Foreground = Res<Brush>("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap };

    internal static TextBlock Warning(string text) => new() { Text = text, Foreground = Res<Brush>("SystemFillColorCautionBrush"), TextWrapping = TextWrapping.Wrap };

    internal static T Res<T>(string key) => (T)Application.Current.Resources[key];
}
