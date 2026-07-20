namespace Velocitex.Core.Profile;

public sealed record AdvancementDefinition(
    string Id,
    string DisplayName,
    string Description,
    string RewardCosmeticId);

public static class AdvancementCatalog
{
    private static readonly AdvancementDefinition[] Definitions =
    {
        new("fresh-from-the-globe", "Fresh from the Globe", "Complete Room 01.", "mint"),
        new("clean-wrapper", "Fresh Coat", "Save a candy look with a pattern other than None.", "rose"),
        new("five-star-batch", "Five-Star Batch", "Complete Rooms 01 through 05.", "stars"),
        new("speeding-sweet", "Speeding Sweet", "Reach a speed of 25 meters per second.", "trail-cyan-glow"),
        new("terminal-sugar", "Terminal Sugar", "Reach a speed of 40 meters per second.", "lightning"),
        new("straight-as-glass", "Straight as Glass", "Complete Room 06 without touching either side wall.", "frost"),
        new("perfect-stop", "Perfect Stop", "Stop inside the marked caramel zone in Room 07 without overshooting.", "caramel-drips"),
        new("blue-streak", "Blue Streak", "Cross every accelerator in Room 08 in one valid sequence.", "trail-blue-sparks"),
        new("double-bounce", "Double Bounce", "Hit two super-elastic surfaces without touching a normal surface between them.", "waves"),
        new("feather-touch", "Feather Touch", "Complete Room 11 without touching a wall.", "trail-cloud"),
        new("against-the-wind", "Against the Wind", "Cross the wind cell without colliding while airborne.", "sky"),
        new("perfect-switch", "Perfect Switch", "Complete Room 14 without changing the rail route you chose first.", "licorice-stripes"),
        new("bullseye", "Bullseye", "Land in the center of the cannon target in Room 16.", "target"),
        new("untouchable", "Untouchable", "Complete Room 17 without being hit by a foam projectile.", "trail-sparks"),
        new("moving-with-it", "Moving With It", "Complete the Room 18 transit without touching either side rail.", "steel"),
        new("piston-perfect", "Piston Perfect", "Enter the Room 19 piston promptly after completing its trajectory setup.", "copper"),
        new("clean-assembly", "Clean Assembly", "Complete Room 20 without a projectile hit or touching a transit side rail.", "trail-amber-bolts"),
        new("full-account", "Full Account", "Charge a Momentum Bank to 100 percent.", "trail-coins"),
        new("sugar-breaker", "Sugar Breaker", "Break every optional brittle barrier in Room 24 during one run.", "cracks"),
        new("vacuum-packed", "Vacuum Packed", "Complete Room 26 without touching the chamber walls.", "trail-vortex"),
    };

    public static IReadOnlyList<AdvancementDefinition> All => Definitions;

    public static AdvancementDefinition? Find(string id)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, id, StringComparison.Ordinal));
    }
}
