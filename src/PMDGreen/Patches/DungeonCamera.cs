using System.Runtime.InteropServices;
using AGBModern;

namespace PMDGreen.Patches;

[StructLayout(LayoutKind.Explicit)]
internal struct DungeonCamera
{
    public const int MapBackground = 3;
    public const int GridBackground = 2;

    private const uint DungeonPointer = 0x0203B418; // gDungeon
    private const uint Offset = 0x181E8;            // Dungeon.unk181e8

    [FieldOffset(0x00)] // UnkDungeonGlobal_unk181E8_sub.cameraPos
    public DungeonPosition Tile;

    [FieldOffset(0x08)] // UnkDungeonGlobal_unk181E8_sub.cameraPixelPos
    public DungeonPosition Position;

    [FieldOffset(0x10)] // UnkDungeonGlobal_unk181E8_sub.cameraTarget
    public uint Target;

    [FieldOffset(0x14)] // UnkDungeonGlobal_unk181E8_sub.unk181FC
    public int MapOffsetY;

    [FieldOffset(0x23)] // UnkDungeonGlobal_unk181E8_sub.allTilesRevealed
    public byte AllTilesRevealed;

    [FieldOffset(0x24)] // UnkDungeonGlobal_unk181E8_sub.unk1820C
    public byte Unk1820C;

    [FieldOffset(0x27)] // UnkDungeonGlobal_unk181E8_sub.showInvisibleTrapsMonsters
    public byte ShowsInvisibles;

    [FieldOffset(0x32)] // UnkDungeonGlobal_unk181E8_sub.unk1821A
    public byte ScrollsBG2;

    public static uint Address => Memory.Peek<uint>(DungeonPointer) + Offset;

    public static ref readonly DungeonCamera Current => ref Memory.Peek<DungeonCamera>(Address);

    public static ref DungeonCamera Changeable => ref Memory.Poke<DungeonCamera>(Address);
}
