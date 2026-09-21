using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class DungeonDarkness
{
    private const uint DrawsWindow = 0x02026E38;     // gDrawWindow
    private const uint ActiveBuffer = 0x02026E3C;    // gWinBufferPtr
    private const uint BufferPointer = 0x02026E58;   // sBufferPtr
    private const uint DimLayerOffsetX = 0x0202D6A4; // gBG1Control.hofs
    private const uint DimLayerOffsetY = 0x0202D6A6; // gBG1Control.vofs
    private const uint RoomCorners = 0x080B8008;     // sRoomCornerDim
    private const int LightCorridor = 1;             // COPY_WINDOW_BG_BUFFER_DIM2
    private const int HeavyCorridor = 2;             // COPY_WINDOW_BG_BUFFER_DIM1
    private const int Room = 3;                      // COPY_WINDOW_BG_BUFFER_ROOM_DIM
    private const ushort CorridorWholeLine = 0x100;
    private const int CornerRows = 16;
    private const int DimWindow = 1;
    private const int DimLayer = 1;
    private const int Everywhere = 1024;

    private static readonly Dictionary<uint, WindowBuffer> Buffers = [];

    public static void Install()
    {
        Funcs.Patches.CopyWindowBgBuffer = CopyWindowBgBuffer(Funcs.Patches.CopyWindowBgBuffer);
        Funcs.Patches.ToggleWindowBgBuffer = ToggleWindowBgBuffer(Funcs.Patches.ToggleWindowBgBuffer);
        GameFrame.Finished += _ => SupplyMargins();
    }

    private static RecompFunc CopyWindowBgBuffer(RecompFunc copyBuffer) => ctx =>
    {
        uint room = ctx.R0;
        int kind = (byte)ctx.R1;
        copyBuffer(ctx);

        uint address = Memory.Peek<uint>(BufferPointer);
        if (!Buffers.TryGetValue(address, out var buffer))
        {
            buffer = new WindowBuffer();
            Buffers.Add(address, buffer);
        }

        buffer.IsRoom = kind == Room;
        buffer.HasSpans = GameFrame.Margins.Width > 0 && kind is (LightCorridor or HeavyCorridor or Room);
        if (!buffer.HasSpans)
        {
            return;
        }

        for (int line = 0; line < buffer.Spans.Length; line++)
        {
            ref ushort horizontal = ref Memory.Poke<ushort>(address + (uint)(line * 4) + 2);
            buffer.Spans[line] = kind == Room ? RoomSpan(room, line) : CorridorSpan(horizontal);
            horizontal = OnScreen(buffer.Spans[line]);
        }
    };

    private static RecompFunc ToggleWindowBgBuffer(RecompFunc toggleBuffer) => ctx =>
    {
        toggleBuffer(ctx);
        if (Buffers.TryGetValue(Memory.Peek<uint>(ActiveBuffer), out var buffer) && buffer.IsRoom)
        {
            MotionReport.MarkWorldWindow(DimWindow);
        }
    };

    private static (int X1, int X2) CorridorSpan(ushort horizontal)
    {
        return horizontal == CorridorWholeLine ? (-Everywhere, Everywhere) : (horizontal >> 8, horizontal & 0xFF);
    }

    private static (int X1, int X2) RoomSpan(uint room, int line)
    {
        int left = Memory.Peek<int>(room), top = Memory.Peek<int>(room + 4);
        int right = Memory.Peek<int>(room + 8), bottom = Memory.Peek<int>(room + 12);
        if (line < top || line >= bottom)
        {
            return (-Everywhere, Everywhere);
        }

        int corner = line - top < CornerRows ? Corner(line - top)
            : bottom - line < CornerRows ? Corner(bottom - line)
            : 0;
        return (right - corner, left + corner);
    }

    private static int Corner(int row) => Memory.Peek<short>(RoomCorners + (uint)(row * 2));

    private static ushort OnScreen((int X1, int X2) span)
    {
        int x1 = Math.Clamp(span.X1, 0, FrameRenderer.Width), x2 = Math.Clamp(span.X2, 0, FrameRenderer.Width);
        bool isLitGapOffScreen = span.X1 > span.X2 && x1 <= x2;
        return isLitGapOffScreen ? Horizontal(0, FrameRenderer.Width) : Horizontal(x1, x2);
    }

    private static ushort Horizontal(int x1, int x2) => (ushort)((x1 << 8) | x2);

    private static void SupplyMargins()
    {
        if (Memory.Peek<byte>(DrawsWindow) == 0
            || !Buffers.TryGetValue(Memory.Peek<uint>(ActiveBuffer), out var buffer)
            || !buffer.HasSpans)
        {
            return;
        }

        for (int line = 0; line < buffer.Spans.Length; line++)
        {
            GameFrame.Margins.SupplyWindow(DimWindow, line, buffer.Spans[line].X1, buffer.Spans[line].X2);
        }

        int firstColumn = Memory.Peek<short>(DimLayerOffsetX) >> 3, firstRow = Memory.Peek<short>(DimLayerOffsetY) >> 3;
        for (int i = 0; i < GameFrame.Margins.ColumnsShown; i++)
        {
            SupplyDimLayer(firstColumn, firstRow, -i);
            SupplyDimLayer(firstColumn, firstRow, GameFrame.ScreenColumns + i);
        }
    }

    private static void SupplyDimLayer(int firstColumn, int firstRow, int column)
    {
        var supplied = GameFrame.Margins.Supply(DimLayer, column);
        for (int row = 0; row < supplied.Length; row++)
        {
            supplied[row] = Tilemaps.Entry(DimLayer, firstColumn + column, firstRow + row);
        }
    }

    private sealed class WindowBuffer
    {
        public bool IsRoom { get; set; }

        public bool HasSpans { get; set; }

        public (int X1, int X2)[] Spans { get; } = new (int X1, int X2)[Video.VisibleLines];
    }
}
