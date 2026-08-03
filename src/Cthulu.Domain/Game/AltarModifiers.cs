namespace Cthulu.Domain.Game;

/// <summary>Per-altar-slot modifiers applied by arcana during a prep day.</summary>
public sealed class AltarSlotModifier
{
    public bool ReverseThinking { get; set; }
    public bool EvilProphecy { get; set; }
}

public sealed class AltarModifiers
{
    private readonly Dictionary<int, AltarSlotModifier> _bySlot = new();

    public AltarSlotModifier Get(int slotIndex)
    {
        if (!_bySlot.TryGetValue(slotIndex, out var mod))
        {
            mod = new AltarSlotModifier();
            _bySlot[slotIndex] = mod;
        }
        return mod;
    }

    public bool HasReverse(int slotIndex) =>
        _bySlot.TryGetValue(slotIndex, out var m) && m.ReverseThinking;

    public bool HasEvilProphecy(int slotIndex) =>
        _bySlot.TryGetValue(slotIndex, out var m) && m.EvilProphecy;

    public void Clear() => _bySlot.Clear();
}
