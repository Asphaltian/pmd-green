using System.Runtime.InteropServices;
using AGBModern;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class Tilemaps
{
    public const int EntriesPerTilemap = 32 * 32;

    private const uint Address = 0x0202B038;              // gBgTilemaps
    private const uint CopyScheduledAddress = 0x0202D238; // sTilemapCopyScheduled
    private const uint FirstScreen = 0x06006000;          // BG_SCREEN_ADDR(12)

    private static readonly bool[] CopiesToShow = new bool[4];

    public static void Install()
    {
        Funcs.Patches.DoScheduledMemCopies = CopyShown(Funcs.Patches.DoScheduledMemCopies);
    }

    public static ref ushort Entry(int bg, int column, int row)
    {
        int index = (bg * EntriesPerTilemap) + ((row & 31) * 32) + (column & 31);
        return ref Memory.Poke<ushort>(Address + (uint)(index * 2));
    }

    public static void Clear(int bg)
    {
        MemoryMarshal.CreateSpan(ref Entry(bg, 0, 0), EntriesPerTilemap).Clear();
        CopiesToShow[bg] = true;
    }

    public static void Show(int bg)
    {
        CopiesToShow[bg] = true;
    }

    public static ref byte IsCopyScheduled(int bg) => ref Memory.Poke<byte>(CopyScheduledAddress + (uint)bg);

    private static RecompFunc CopyShown(RecompFunc copy) => ctx =>
    {
        copy(ctx);
        Scheduler.RunUntimed(() =>
        {
            for (int bg = 0; bg < CopiesToShow.Length; bg++)
            {
                if (!CopiesToShow[bg])
                {
                    continue;
                }

                CopiesToShow[bg] = false;
                uint screen = FirstScreen + (uint)(bg * EntriesPerTilemap * 2);
                for (int i = 0; i < EntriesPerTilemap; i++)
                {
                    Memory.Write16(screen + (uint)(i * 2), Memory.Peek<ushort>(Address + (uint)(((bg * EntriesPerTilemap) + i) * 2)));
                }
            }
        });
    };
}
