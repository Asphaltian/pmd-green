using System.Runtime.InteropServices;

namespace PMDGreen.Patches;

[StructLayout(LayoutKind.Explicit)]
internal struct Entity
{
    public const uint Nothing = 0;           // ENTITY_NOTHING
    public const uint Trap = 2;              // ENTITY_TRAP
    public const uint PositionOffset = 0x0C; // Entity.pixelPos

    [FieldOffset(0x00)] // Entity.type
    public uint Type;

    [FieldOffset(0x0C)] // Entity.pixelPos
    public PixelPosition Position;

    [FieldOffset(0x1C)] // Entity.unk1C
    public int Height;

    [FieldOffset(0x20)] // Entity.isVisible
    public byte IsVisible;

    [FieldOffset(0x26)] // Entity.spawnGenID
    public ushort SpawnGenID;

    [FieldOffset(0x38)] // Entity.axObj.axdata.sub1.shadow
    public DungeonPosition Shadow;

    [FieldOffset(0x70)] // Entity.axObj.info
    public uint Info;
}
