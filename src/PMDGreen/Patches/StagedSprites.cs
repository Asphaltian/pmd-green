using System.Runtime.InteropServices;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class StagedSprites
{
    public const uint Address = 0x020262A8;       // sUnknown_20262A8
    public const uint ListAddress = 0x020256A0;   // sSpriteList
    public const uint ListSize = 0x808;           // sizeof(SpriteList)
    public const uint LinksAddress = 0x02025EA8;  // sUnknown_2025EA8
    public const int LinkSize = 8;                // sizeof(UnkSpriteLink)
    public const int Orders = 256;
    public const int Size = 8;                    // sizeof(SpriteOAM)
    public const uint CountAddress = 0x020266A8;  // sSpriteCount

    private const uint CopiedCountAddress = 0x02025670; // sOAMSpriteCount

    private const int MostSprites = 128;

    private static readonly List<Extra> Extras = [];
    private static readonly HashSet<int> Hidden = [];
    private static readonly byte[] SeparateLists = new byte[Address - ListAddress];
    private static readonly byte[] GameStaging = new byte[CountAddress + sizeof(int) - ListAddress];

    private static RecompFunc _linkSprites = null!;
    private static bool _hasSeparateLists;
    private static bool _isShowingExtras;
    private static int _setAsideCount;
    private static ushort _copiedCount;

    public readonly record struct Link(uint Next, uint Sprite);

    public readonly record struct Staged(int Sprites, int Extras);

    private sealed class Extra(int order, (int, int) stagedAfter, SpriteOAM sprite, bool isInWorld, Displacement own)
    {
        public int Order { get; } = order;

        public (int Sprite, int SetAside) StagedAfter { get; } = stagedAfter;

        public SpriteOAM Sprite = sprite;

        public bool IsInWorld = isInWorld;

        public Displacement Own = own;
    }

    public static int Count => Memory.Peek<int>(CountAddress);

    public static Staged SoFar => new(Count, Extras.Count);

    public static IEnumerable<(SpriteOAM Sprite, int Order)> ExtraSprites => Extras.Select(extra => (extra.Sprite, extra.Order));

    public static int ShownOffset { get; set; }

    private static Span<byte> Staging => MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(ListAddress), GameStaging.Length);

    private static Span<byte> Lists => MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(ListAddress), SeparateLists.Length);

    public static void Install()
    {
        _linkSprites = Funcs.Patches.sub_8005180;
        Funcs.Patches.sub_8005180 = ctx =>
        {
            if (!_hasSeparateLists)
            {
                Lists.CopyTo(SeparateLists);
                _hasSeparateLists = true;
            }

            _linkSprites(ctx);
        };

        var reset = Funcs.Patches.ResetSprites;
        Funcs.Patches.ResetSprites = ctx =>
        {
            reset(ctx);
            Clear();
        };
    }

    public static SpriteOAM Get(int index) => Memory.Peek<SpriteOAM>(Address + (uint)(index * Size));

    public static void MoveX(Staged first, int shift)
    {
        for (int i = first.Sprites, count = Count; i < count; i++)
        {
            ref var sprite = ref Memory.Poke<SpriteOAM>(Address + (uint)(i * Size));
            sprite.SetX(sprite.Attribute1 + shift);
        }

        for (int i = first.Extras; i < Extras.Count; i++)
        {
            Extras[i].Sprite.SetX(Extras[i].Sprite.Attribute1 + shift);
        }
    }

    public static void SetAside(int first)
    {
        var aside = new Stack<Extra>();
        for (int i = Count - 1; i >= first; i--)
        {
            uint link = LinksAddress + (uint)(i * LinkSize);
            int order = OrderLinkedTo(link);
            if (order < 0)
            {
                break;
            }

            Memory.Poke<uint>(ListAddress + (uint)(order * LinkSize)) = Memory.Peek<uint>(link);
            var (isInWorld, own) = MotionReport.TakeMark(i);
            aside.Push(new Extra(order, (first - 1, _setAsideCount + i - first + 1), Get(i), isInWorld, own));
            Memory.Poke<int>(CountAddress) = i;
        }

        _setAsideCount += aside.Count;
        Extras.AddRange(aside);
    }

    public static void Hide(int first, int end)
    {
        for (int i = first; i < end; i++)
        {
            Hidden.Add(i);
        }
    }

    public static void Forget(Staged first)
    {
        Extras.RemoveRange(first.Extras, Extras.Count - first.Extras);
        Hidden.RemoveWhere(i => i >= first.Sprites);
    }

    public static void MarkExtras(int first, Displacement own)
    {
        for (int i = first; i < Extras.Count; i++)
        {
            if (!Extras[i].IsInWorld)
            {
                Extras[i].IsInWorld = true;
                Extras[i].Own = own;
            }
        }
    }

    public static void ShowExtras(RecompContext ctx, RecompFunc copy)
    {
        if (Extras.Count == 0 && Hidden.Count == 0)
        {
            return;
        }

        Staging.CopyTo(GameStaging);
        _copiedCount = Memory.Peek<ushort>(CopiedCountAddress);
        _isShowingExtras = true;
        int gameCount = Count;
        var stagedAfter = new Dictionary<uint, (int, int)>();
        Scheduler.RunUntimed(() =>
        {
            if (_hasSeparateLists)
            {
                SeparateLists.CopyTo(Lists);
            }

            foreach (int i in Hidden)
            {
                ref var sprite = ref Memory.Poke<SpriteOAM>(Address + (uint)(i * Size));
                sprite.Attribute0 = (ushort)((sprite.Attribute0 & ~SpriteOAM.AffineFlag) | SpriteOAM.DisableFlag);
            }

            foreach (var extra in Extras)
            {
                int count = Count;
                if (count >= MostSprites)
                {
                    break;
                }

                Memory.Poke<SpriteOAM>(Address + (uint)(count * Size)) = extra.Sprite;
                uint link = LinksAddress + (uint)(count * LinkSize);
                uint previous = ListAddress + (uint)(extra.Order * LinkSize);
                while (Memory.Peek<uint>(previous) is var next && next != 0 && next - ListAddress >= ListSize)
                {
                    int index = (int)(next - LinksAddress) / LinkSize;
                    var nextStagedAfter = index < gameCount ? (index, 0) : stagedAfter[next];
                    if (nextStagedAfter.CompareTo(extra.StagedAfter) < 0)
                    {
                        break;
                    }

                    previous = next;
                }

                Memory.Poke<uint>(link) = Memory.Peek<uint>(previous);
                Memory.Poke<uint>(previous) = link;
                stagedAfter[link] = extra.StagedAfter;
                MotionReport.Mark(count, extra.IsInWorld, extra.Own);
                Memory.Poke<int>(CountAddress) = count + 1;
            }

            if (_hasSeparateLists)
            {
                _linkSprites(ctx);
            }

            copy(ctx);
        });
    }

    public static void ShowGameSprites()
    {
        if (_isShowingExtras)
        {
            GameStaging.CopyTo(Staging);
            Memory.Poke<ushort>(CopiedCountAddress) = _copiedCount;
            _isShowingExtras = false;
        }

        Clear();
    }

    private static void Clear()
    {
        Extras.Clear();
        Hidden.Clear();
        _hasSeparateLists = false;
        _setAsideCount = 0;
    }

    private static int OrderLinkedTo(uint link)
    {
        for (int order = 0; order < Orders; order++)
        {
            if (Memory.Peek<uint>(ListAddress + (uint)(order * LinkSize)) == link)
            {
                return order;
            }
        }

        return -1;
    }
}
