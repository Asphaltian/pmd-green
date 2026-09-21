using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class EntitySlots
{
    public static event Action? Emptied;

    public static void Install()
    {
        var empty = Funcs.Patches.sub_804513C;
        Funcs.Patches.sub_804513C = ctx =>
        {
            empty(ctx);
            Emptied?.Invoke();
        };
    }
}
