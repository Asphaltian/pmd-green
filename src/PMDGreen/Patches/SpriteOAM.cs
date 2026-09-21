using System.Runtime.InteropServices;

namespace PMDGreen.Patches;

[StructLayout(LayoutKind.Sequential)]
internal struct SpriteOAM
{
    public const int AffineFlag = 0x100;  // SPRITEOAM_MASK_AFFINEMODE1
    public const int DisableFlag = 0x200; // SPRITEOAM_MASK_AFFINEMODE2
    public const int XMask = 0x1FF;       // SPRITEOAM_MAX_X

    private const int WorkingYMask = 0xFFF; // SPRITEOAM_MAX_WORKING_Y
    private const int WorkingYShift = 4;    // SPRITEOAM_SHIFT_WORKING_Y

    public ushort Attribute0;
    public ushort Attribute1;
    public ushort Attribute2;
    public ushort WorkingY;

    public void SetX(int x)
    {
        Attribute1 = (ushort)((Attribute1 & ~XMask) | (x & XMask));
    }

    public void SetWorkingY(int y)
    {
        WorkingY = (ushort)((WorkingY & ((1 << WorkingYShift) - 1)) | ((y & WorkingYMask) << WorkingYShift));
    }
}
