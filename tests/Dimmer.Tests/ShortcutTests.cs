using Dimmer.Core;

namespace Dimmer.Tests;

public class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Alt+B", Modifiers.Ctrl | Modifiers.Alt, 'B')]
    [InlineData("win + shift + f1", Modifiers.Win | Modifiers.Shift, 0x70)]
    [InlineData("F13", Modifiers.None, 0x7C)]
    [InlineData("Ctrl+PageUp", Modifiers.Ctrl, 0x21)]
    [InlineData("Ctrl+1", Modifiers.Ctrl, '1')]
    [InlineData("Alt+2", Modifiers.Alt, '2')]
    [InlineData("Shift+4", Modifiers.Shift, '4')]
    [InlineData("Win+8", Modifiers.Win, '8')]
    public void 文字列を読む(string text, Modifiers modifiers, int key) => Assert.Equal(new Shortcut(modifiers, key), Shortcut.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Unknown")]
    [InlineData("None+A")]
    public void 読めなければnull(string text) => Assert.Null(Shortcut.Parse(text));

    [Theory]
    [InlineData("Ctrl+B", true)]
    [InlineData("Win+F1", true)]
    [InlineData("B", false)]           // 修飾キーが必須
    [InlineData("F12", false)]         // F1〜F12 は修飾キーが必須
    [InlineData("F13", true)]          // F13〜F24 は修飾キーなしでよい
    [InlineData("F24", true)]
    [InlineData("PageUp", false)]
    public void 修飾キーが必須で_F13からF24は例外(string text, bool valid) => Assert.Equal(valid, Shortcut.Parse(text)!.IsValid);

    [Theory]
    [InlineData('0')]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('4')]
    [InlineData('8')]
    [InlineData('9')]
    public void 数字キーを書き出して読み直すと同じ(char key)
    {
        var s = new Shortcut(Modifiers.Ctrl, key);
        Assert.Equal(s, Shortcut.Parse(s.ToString()));
    }

    [Fact]
    public void 書き出して読み直すと同じ()
    {
        var s = new Shortcut(Modifiers.Ctrl | Modifiers.Alt | Modifiers.Shift | Modifiers.Win, 0x22);
        Assert.Equal("Ctrl+Alt+Shift+Win+PageDown", s.ToString());
        Assert.Equal(s, Shortcut.Parse(s.ToString()));
    }

    [Fact]
    public void 重複を検出する()
    {
        var dup = ShortcutValidation.Duplicates([Shortcut.Parse("Ctrl+A")!, Shortcut.Parse("ctrl+a")!, Shortcut.Parse("Ctrl+B")!, Shortcut.Parse("F13")!]);
        Assert.Equal([Shortcut.Parse("Ctrl+A")!], dup);
    }
}
