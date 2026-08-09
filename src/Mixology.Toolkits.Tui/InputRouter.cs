using Terminal.Gui.Input;

namespace Mixology.Toolkits.Tui;

public readonly record struct InputOwnership(bool CapturesText, bool HandlesBack)
{
    public static InputOwnership Browse { get; } = new(false, false);
    public static InputOwnership Edit { get; } = new(true, true);
}

public static class InputRouter
{
    public static bool Dispatch(
        Key key,
        InputOwnership ownership,
        Func<Key, bool> local,
        Func<Key, bool> global)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(global);

        if (ownership.CapturesText || (ownership.HandlesBack && key == Key.Esc))
        {
            _ = local(key);
            key.Handled = true;
            return true;
        }

        if (local(key) || global(key))
        {
            key.Handled = true;
            return true;
        }

        return false;
    }
}
