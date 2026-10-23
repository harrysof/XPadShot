namespace XPadShot.Models;

/// <summary>A selectable XInput position for the player picker.
/// Value is 1-4, matching the "Player N" labels and XInput slot N-1.</summary>
public sealed record PlayerOption(int Value, string Label);
