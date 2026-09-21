using AGBModern;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class WideSprites
{
    private const int Nearest = -64;
    private const int Farthest = 239;
    private const int XBias = 0x100;
    private const uint PoseFlags2Offset = 0x6; // ax_pose.flags2
    private const uint MaskOffset = 0x2;       // unkStruct_2039DB0.unk2
    private const uint MaskValueOffset = 0x8;  // unkStruct_2039DB0.unk8

    public static void Install()
    {
        Funcs.Patches.AddAxSprite = AddAxSprite(Funcs.Patches.AddAxSprite);
    }

    private static RecompFunc AddAxSprite(RecompFunc addSprite) => ctx =>
    {
        uint pose = ctx.R0, axData = ctx.R1, masks = ctx.R3;
        var before = StagedSprites.SoFar;
        addSprite(ctx);

        int margin = GameFrame.Margins.Width;
        if (margin == 0 || StagedSprites.Count != before.Sprites)
        {
            return;
        }

        int x = StagedX(pose, axData, masks);
        int shown = x - StagedSprites.ShownOffset;
        if (x is >= Nearest and <= Farthest || shown < Nearest - margin || shown > Farthest + margin)
        {
            return;
        }

        int shift = x < Nearest ? Nearest - x : Farthest - x;
        Scheduler.RunUntimed(() =>
        {
            ref short positionX = ref Memory.Poke<short>(axData);
            positionX += (short)shift;
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (pose, axData, 0, masks);
            addSprite(ctx);
            positionX -= (short)shift;
        });

        StagedSprites.MoveX(before, -shift);
        StagedSprites.SetAside(before.Sprites);
    };

    private static int StagedX(uint pose, uint axData, uint masks)
    {
        int flags2 = Memory.Peek<ushort>(pose + PoseFlags2Offset);
        if (masks != 0)
        {
            flags2 = (flags2 & Memory.Peek<ushort>(masks + MaskOffset)) | Memory.Peek<ushort>(masks + MaskValueOffset);
        }

        return (flags2 & SpriteOAM.XMask) + Memory.Peek<short>(axData) - XBias;
    }
}
