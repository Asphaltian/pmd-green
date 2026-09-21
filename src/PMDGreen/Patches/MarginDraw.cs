using System.Runtime.InteropServices;
using AGBModern;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class MarginDraw
{
    private const uint EWRAMStart = 0x02000000;
    private const int EWRAMSize = 0x40000;
    private const uint IWRAMStart = 0x03000000;
    private const int IWRAMSize = 0x8000;
    private const uint UploadsEndPointer = 0x0203B074; // sUnknown_203B074
    private const uint UploadSize = 0xC;               // sizeof(unkStruct_20266B0)
    private const uint UploadSourceOffset = 0x4;       // unkStruct_20266B0.src
    private const uint UploadDestinationOffset = 0x8;  // unkStruct_20266B0.dest

    private static readonly Stack<byte[]> Unused = [];
    private static readonly List<Upload> Uploads = [];

    public readonly record struct Before(byte[] RAM, StagedSprites.Staged Sprites);

    private readonly record struct Upload(int ByteCount, uint Source, uint Destination);

    public static bool IsDrawing { get; private set; }

    private static Span<byte> EWRAM => MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(EWRAMStart), EWRAMSize);

    private static Span<byte> IWRAM => MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(IWRAMStart), IWRAMSize);

    public static void Install()
    {
        Funcs.Patches.sub_8005304 = UploadSprites(Funcs.Patches.sub_8005304);
    }

    public static Before Save()
    {
        return new Before(SaveRAM(), StagedSprites.SoFar);
    }

    public static void Forget(Before before)
    {
        Unused.Push(before.RAM);
    }

    public static bool DrawAgain(RecompContext ctx, Before before, int shift, Action draw)
    {
        var (r0, r1, r2, r3, r12, r14) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3, ctx.R12, ctx.R14);
        var (n, z, c, v) = (ctx.N, ctx.Z, ctx.C, ctx.V);
        byte[] after = SaveRAM();
        StagedSprites.Forget(before.Sprites);
        Restore(before.RAM);

        var first = StagedSprites.SoFar;
        uint uploadsEnd = Memory.Peek<uint>(UploadsEndPointer);
        int offset = StagedSprites.ShownOffset;
        StagedSprites.ShownOffset = offset - shift;
        IsDrawing = true;
        try
        {
            Scheduler.RunUntimed(draw);
        }
        finally
        {
            IsDrawing = false;
            StagedSprites.ShownOffset = offset;
        }

        StagedSprites.MoveX(first, shift);
        StagedSprites.SetAside(first.Sprites);
        int uploaded = Uploads.Count;
        for (uint entry = uploadsEnd, end = Memory.Peek<uint>(UploadsEndPointer); entry < end; entry += UploadSize)
        {
            Uploads.Add(new Upload(Memory.Peek<int>(entry), Memory.Peek<uint>(entry + UploadSourceOffset), Memory.Peek<uint>(entry + UploadDestinationOffset)));
        }

        Restore(after);
        StagedSprites.Hide(first.Sprites, StagedSprites.Count);
        Unused.Push(after);
        Forget(before);
        (ctx.R0, ctx.R1, ctx.R2, ctx.R3, ctx.R12, ctx.R14) = (r0, r1, r2, r3, r12, r14);
        (ctx.N, ctx.Z, ctx.C, ctx.V) = (n, z, c, v);
        return Uploads.Count != uploaded;
    }

    private static byte[] SaveRAM()
    {
        var ram = Unused.Count > 0 ? Unused.Pop() : new byte[EWRAMSize + IWRAMSize];
        EWRAM.CopyTo(ram);
        IWRAM.CopyTo(ram.AsSpan(EWRAMSize));
        return ram;
    }

    private static void Restore(byte[] ram)
    {
        ram.AsSpan(0, EWRAMSize).CopyTo(EWRAM);
        ram.AsSpan(EWRAMSize).CopyTo(IWRAM);
    }

    private static RecompFunc UploadSprites(RecompFunc upload) => ctx =>
    {
        upload(ctx);
        if (Uploads.Count == 0)
        {
            return;
        }

        Scheduler.RunUntimed(() =>
        {
            foreach (var (byteCount, source, destination) in Uploads)
            {
                for (int i = 0; i < byteCount; i += sizeof(uint))
                {
                    Memory.Write32(destination + (uint)i, source != 0 ? Memory.Read32(source + (uint)i) : 0);
                }
            }
        });
        Uploads.Clear();
    };
}
