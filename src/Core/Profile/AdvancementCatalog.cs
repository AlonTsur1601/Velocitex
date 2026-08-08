namespace Velocitex.Core.Profile;

public sealed record AdvancementDefinition(
    string Id,
    string DisplayName,
    string Description,
    string? RewardCosmeticId);

public static class AdvancementCatalog
{
    private static readonly AdvancementDefinition[] Definitions =
    {
        new("fresh-from-the-globe", "Fresh from the Globe", "Complete Room 01.", "mint"),
        new("clean-wrapper", "Fresh Coat", "Save a candy look with a pattern.", "rose"),
        new("five-star-batch", "Five-Star Batch", "Complete Rooms 01 through 05.", "stars"),
        new("speeding-sweet", "Speeding Sweet", "Reach a speed of 25 meters per second.", "trail-cyan-glow"),
        new("terminal-sugar", "Terminal Sugar", "Reach a speed of 40 meters per second.", "lightning"),
        new("straight-as-glass", "Straight as Glass", "Complete Room 06 without touching either side wall.", "frost"),
        new("perfect-stop", "Perfect Stop", "Stop inside the marked caramel zone in Room 07 without overshooting.", "caramel-drips"),
        new("blue-streak", "No Brakes", "Complete Room 08 within 15 seconds of the first accelerator without touching a side rail.", "trail-blue-sparks"),
        new("double-bounce", "Double Bounce", "Hit two super-elastic surfaces without touching a normal surface between them.", "waves"),
        new("feather-touch", "Feather Touch", "Complete Room 11 without touching a wall.", "trail-cloud"),
        new("against-the-wind", "Against the Wind", "Cross the wind cell in Room 13 without colliding while airborne.", "sky"),
        new("perfect-switch", "Perfect Switch", "Complete Room 14 without changing the rail route you chose first.", "licorice-stripes"),
        new("bullseye", "Thread the Needle", "Pass within 25 centimeters of the center of Room 16's acceleration ring, then complete the room.", "target"),
        new("untouchable", "Clean Crossfire", "Cross all 12 cannon lanes in Room 17 without a projectile hit or touching a landing rail.", "trail-sparks"),
        new("moving-with-it", "Moving With It", "Complete the Room 18 transit without touching either side rail.", "steel"),
        new("piston-perfect", "Piston Perfect", "Enter the Room 19 piston promptly after completing its trajectory setup.", "copper"),
        new("clean-assembly", "Clean Assembly", "Complete Room 20 without a projectile hit or touching a transit side rail.", "trail-amber-bolts"),
        new("full-account", "Full Account", "Charge the Momentum Bank in Room 23 to 100 percent.", "trail-coins"),
        new("sugar-breaker", "Sugar Breaker", "Break every optional brittle barrier in Room 24 during one run.", "cracks"),
        new("vacuum-packed", "Vacuum Packed", "Complete Room 26 without touching the chamber walls.", "trail-vortex"),
        new("flawless-campaign", "Campaign Complete", "Complete the Normal campaign.", "bronze-crown"),
        new("hard-mode-complete", "Hard Candy", "Complete Hard Mode.", "silver-crown"),
        new("extreme-mode-complete", "Extreme Candy", "Complete Extreme Mode.", "gold-crown"),
        new("jawbreaker", "Jawbreaker", "Complete Hard Mode in one attempt without returning to a checkpoint.", "sparkling"),
        new("smooth-operator", "Smooth Operator", "Complete Room 29 without touching a side rail after the platform starts.", "silk-blue"),
        new("final-inspection", "Final Inspection", "Cross all three flight gates in Room 30 without being hit by an Interference Cannon.", "inspection-grid"),
    };

    public static IReadOnlyList<AdvancementDefinition> All => Definitions;

    public static AdvancementDefinition? Find(string id)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, id, StringComparison.Ordinal));
    }
}
