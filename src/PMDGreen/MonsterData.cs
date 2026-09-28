using AGBModern;
using PMDGreen.Patches;

namespace PMDGreen;

/// <summary>
/// <c>MonsterData</c> keeps your own data for each monster in a dungeon. A monster gets a new
/// <typeparamref name="T"/> the first time you ask for its data, and keeps it until the floor ends.
/// Keep in mind that this includes your team, so their data starts over on every floor too. Don't
/// forget to create it when your mod loads.
/// </summary>
/// <example>
/// <code>
/// private sealed class Experience
/// {
///     public int Points;
/// }
///
/// private static readonly MonsterData&lt;Experience&gt; Gained = new();
///
/// // AddExpPoints(pokemon, target, exp)
/// mod.Hook(ref Funcs.Patches.AddExpPoints, ctx => Gained[ctx.R1].Points += (int)ctx.R2);
/// </code>
/// </example>
public sealed class MonsterData<T>
    where T : class, new()
{
    private readonly Dictionary<ushort, T> _data = [];

    /// <summary>Makes an empty set of data.</summary>
    public MonsterData()
    {
        EntitySlots.Emptied += _data.Clear;
    }

    /// <summary>The data of the monster whose <c>Entity</c> is at <paramref name="monster"/>. Throws if there's no monster there.</summary>
    public T this[uint monster]
    {
        get
        {
            ushort id = Memory.Peek<Entity>(monster).SpawnGenID;
            if (id == 0)
            {
                throw new ArgumentException($"0x{monster:X8} is not a monster.", nameof(monster));
            }

            if (!_data.TryGetValue(id, out var data))
            {
                data = new T();
                _data.Add(id, data);
            }

            return data;
        }
    }
}
