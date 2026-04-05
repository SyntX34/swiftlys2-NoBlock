namespace NoBlock;
public class NoBlockConfig
{
    /// <summary>HE grenade passes through players globally.</summary>
    public bool HEGrenade { get; set; } = true;

    /// <summary>Flashbang passes through players globally.</summary>
    public bool Flashbang { get; set; } = true;

    /// <summary>Smoke grenade passes through players globally.</summary>
    public bool SmokeGrenade { get; set; } = true;

    /// <summary>Molotov / Incendiary passes through players globally.</summary>
    public bool Molotov { get; set; } = true;

    /// <summary>Decoy grenade passes through players globally.</summary>
    public bool Decoy { get; set; } = true;

    /// <summary>
    /// Seconds of player no-block granted when a player uses !noblock.
    /// They will pass through other players for this duration.
    /// </summary>
    public float NoBlockTimer { get; set; } = 5.0f;

    /// <summary>
    /// Cooldown in seconds before the same player can trigger !noblock again.
    /// </summary>
    public float NoBlockCooldownTimer { get; set; } = 10.0f;

    /// <summary>
    /// When true, a player on a ladder automatically receives no-block
    /// (passes through others) until they leave the ladder.
    /// </summary>
    public bool Ladder { get; set; } = true;

    /// <summary>Chat prefix prepended to every plugin message.</summary>
    public string ChatPrefix { get; set; } = " \x0C[NoBlock]\x01 ";
}