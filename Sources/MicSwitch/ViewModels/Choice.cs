namespace MicSwitch.ViewModels;

/// <summary>An item for a ComboBox: the stored value plus user-facing text.</summary>
public sealed record Choice<T>(T Value, string Name, string? Description = null)
{
    public override string ToString() => Name;
}
