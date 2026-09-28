using System.Runtime.InteropServices;
using System.Text;
using AGBModern;
using PMDGreen.Patches;
using RecompiledFuncs;

namespace PMDGreen;

/// <summary>
/// <c>FileSystem</c> lets you swap the files in the game's archives for your own, like a map's
/// palette. You'll find each archive's address in <see cref="Data"/>, like <c>gGroundFileArchive</c>,
/// and the names of its files in the decompilation's tables, like <c>gGroundFiles</c>.
/// </summary>
/// <example>
/// <code>
/// public void Load(Mod mod)
/// {
///     // Give Pokémon Square the palette from our mod
///     FileSystem.Replace(Data.gGroundFileArchive, "T01P01", mod.ReadFile("files/T01P01.bpl"));
/// }
/// </code>
/// </example>
public static class FileSystem
{
    private const string ArchiveMagic = "pksdir0"; // sPksDir0
    private const uint FileSize = 8;               // sizeof(File)

    private static readonly Dictionary<uint, uint> Replacements = [];

    [StructLayout(LayoutKind.Explicit)]
    private struct FileArchive
    {
        [FieldOffset(0x08)] // FileArchive.count
        public int Count;

        [FieldOffset(0x0C)] // FileArchive.entries
        public uint Entries;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct File
    {
        [FieldOffset(0x00)] // File.name
        public uint Name;

        [FieldOffset(0x04)] // File.data
        public uint Data;
    }

    /// <summary>
    /// Makes the game read <paramref name="content"/> whenever it opens the file called
    /// <paramref name="name"/> in <paramref name="archive"/>, and returns the address the content
    /// is at. If your file has pointers in it, like the one after a <c>SIRO</c> header, point them
    /// at that address with <see cref="Memory.Poke{T}"/>. Throws if the archive has no file by that
    /// name.
    /// </summary>
    public static uint Replace(uint archive, string name, ReadOnlySpan<byte> content)
    {
        uint entry = Find(archive, name);
        uint data = Memory.Allocate(content.Length);
        content.CopyTo(MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(data), content.Length));

        uint file = Memory.Allocate((int)FileSize);
        Memory.Poke<File>(file) = Memory.Peek<File>(entry) with { Data = data };
        Replacements[entry] = file;
        return data;
    }

    internal static void Install()
    {
        var open = Funcs.Patches.OpenFile;
        Funcs.Patches.OpenFile = ctx =>
        {
            open(ctx);

            if (ctx.R0 != 0 && Replacements.TryGetValue(Memory.Peek<OpenedFile>(ctx.R0).File, out uint file))
            {
                ref var opened = ref Memory.Poke<OpenedFile>(ctx.R0);
                opened = opened with { File = file };
            }
        };
    }

    private static uint Find(uint archive, string name)
    {
        if (!IsString(archive, ArchiveMagic))
        {
            throw new ArgumentException($"0x{archive:X8} is not a file archive.", nameof(archive));
        }

        var fileArchive = Memory.Peek<FileArchive>(archive);
        for (int i = 0; i < fileArchive.Count; i++)
        {
            uint entry = fileArchive.Entries + ((uint)i * FileSize);
            if (IsString(Memory.Peek<File>(entry).Name, name))
            {
                return entry;
            }
        }

        throw new ArgumentException($"The archive at 0x{archive:X8} has no file called {name}.", nameof(name));
    }

    private static bool IsString(uint address, string text)
    {
        return Memory.TryGetSpan(address, text.Length + 1, out var bytes)
            && bytes[^1] == 0
            && Encoding.ASCII.GetString(bytes[..^1]) == text;
    }
}
