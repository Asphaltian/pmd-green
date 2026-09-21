using AGBModern;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class MusicVolume
{
    private const uint TrackCountOffset = 0x08;  // MusicPlayerInfo.trackCount
    private const uint TracksOffset = 0x2C;      // MusicPlayerInfo.tracks
    private const uint TrackSize = 0x50;         // sizeof(MusicPlayerTrack)
    private const uint VolumeRightOffset = 0x10; // MusicPlayerTrack.volMR
    private const uint VolumeLeftOffset = 0x11;  // MusicPlayerTrack.volML
    private const byte VolumeSet = 0x01;         // MPT_FLG_VOLSET
    private const byte VolumeChanged = 0x03;     // MPT_FLG_VOLCHG
    private const byte Exists = 0x80;            // MPT_FLG_EXIST

    private static readonly uint[] MusicPlayers = [Data.gMPlayInfo_BGM, Data.gMPlayInfo_Fanfare];

    private static volatile int _level = 100;
    private static volatile bool _changed;

    public static void Set(int level)
    {
        level = Math.Clamp(level, 0, 100);
        if (level != _level)
        {
            _level = level;
            _changed = true;
        }
    }

    public static void Install()
    {
        var setVolume = Funcs.Patches.TrkVolPitSet;
        Funcs.Patches.TrkVolPitSet = ctx =>
        {
            uint player = ctx.R0;
            uint track = ctx.R1;
            bool volumeSet = (Memory.Peek<byte>(track) & VolumeSet) != 0;
            setVolume(ctx);

            int level = _level;
            if (volumeSet && level < 100 && MusicPlayers.Contains(player))
            {
                ref byte right = ref Memory.Poke<byte>(track + VolumeRightOffset);
                ref byte left = ref Memory.Poke<byte>(track + VolumeLeftOffset);
                right = (byte)(right * level / 100);
                left = (byte)(left * level / 100);
            }
        };

        GameFrame.Finished += _ =>
        {
            if (_changed)
            {
                _changed = false;
                foreach (uint player in MusicPlayers)
                {
                    uint tracks = Memory.Peek<uint>(player + TracksOffset);
                    for (uint i = 0; i < Memory.Peek<byte>(player + TrackCountOffset); i++)
                    {
                        ref byte flags = ref Memory.Poke<byte>(tracks + (i * TrackSize));
                        if ((flags & Exists) != 0)
                        {
                            flags |= VolumeChanged;
                        }
                    }
                }
            }
        };
    }
}
