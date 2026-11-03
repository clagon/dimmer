using System.Windows.Input;

namespace Dimmer;

/// <summary>トレイアイコンの左クリック用の最小の ICommand。</summary>
public sealed class Command(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
