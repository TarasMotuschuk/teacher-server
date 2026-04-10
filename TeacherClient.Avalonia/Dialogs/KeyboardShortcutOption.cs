using MarcusW.VncClient;

namespace TeacherClient.CrossPlatform.Dialogs;

internal sealed record KeyboardShortcutOption(string Label, params KeySymbol[] ShortcutKeys)
{
    public IReadOnlyList<KeySymbol> Keys { get; } = ShortcutKeys;

    public override string ToString() => Label;
}
