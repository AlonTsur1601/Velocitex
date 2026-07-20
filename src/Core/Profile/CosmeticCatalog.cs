namespace Velocitex.Core.Profile;

public enum CosmeticKind
{
    Color,
    Pattern,
    Trail,
}

public sealed record CosmeticDefinition(
    string Id,
    CosmeticKind Kind,
    string DisplayName,
    string PreviewValue,
    bool UnlockedByDefault);

public static class CosmeticCatalog
{
    private static readonly CosmeticDefinition[] Definitions =
    {
        new("cherry", CosmeticKind.Color, "Cherry", "#D12B3F", true),
        new("blueberry", CosmeticKind.Color, "Blueberry", "#315FC8", true),
        new("lime", CosmeticKind.Color, "Lime", "#67B84B", true),
        new("lemon", CosmeticKind.Color, "Lemon", "#E5C84A", true),
        new("grape", CosmeticKind.Color, "Grape", "#7546A8", true),
        new("orange", CosmeticKind.Color, "Orange", "#DB7B31", true),
        new("vanilla", CosmeticKind.Color, "Vanilla", "#E8D9B8", true),
        new("charcoal", CosmeticKind.Color, "Charcoal", "#343940", true),
        new("none", CosmeticKind.Pattern, "No Pattern", "none", true),
        new("halves", CosmeticKind.Pattern, "Two Halves", "halves", true),
        new("spiral", CosmeticKind.Pattern, "Spiral", "spiral", true),
        new("rings", CosmeticKind.Pattern, "Rings", "rings", true),
        new("sugar-dots", CosmeticKind.Pattern, "Sugar Dots", "sugar-dots", true),
        new("off", CosmeticKind.Trail, "Trail Off", "#00000000", true),
        new("trail-white", CosmeticKind.Trail, "White Trail", "#F2F1E9", true),
        new("trail-cyan", CosmeticKind.Trail, "Cyan Trail", "#18A99C", true),
        new("trail-pink", CosmeticKind.Trail, "Pink Trail", "#F080B7", true),
        new("trail-gold", CosmeticKind.Trail, "Gold Trail", "#E6B84B", true),
        new("mint", CosmeticKind.Color, "Mint", "#75CDB0", false),
        new("rose", CosmeticKind.Color, "Rose", "#D96C82", false),
        new("frost", CosmeticKind.Color, "Frost", "#BDE8ED", false),
        new("sky", CosmeticKind.Color, "Sky", "#79BCE8", false),
        new("steel", CosmeticKind.Color, "Steel", "#8796A3", false),
        new("copper", CosmeticKind.Color, "Copper", "#B66D3D", false),
        new("stars", CosmeticKind.Pattern, "Stars", "stars", false),
        new("lightning", CosmeticKind.Pattern, "Lightning", "lightning", false),
        new("caramel-drips", CosmeticKind.Pattern, "Caramel Drips", "caramel-drips", false),
        new("waves", CosmeticKind.Pattern, "Waves", "waves", false),
        new("licorice-stripes", CosmeticKind.Pattern, "Licorice Stripes", "licorice-stripes", false),
        new("target", CosmeticKind.Pattern, "Target", "target", false),
        new("cracks", CosmeticKind.Pattern, "Sugar Cracks", "cracks", false),
        new("pearl", CosmeticKind.Pattern, "Pearl Finish", "pearl", true),
        new("trail-cyan-glow", CosmeticKind.Trail, "Cyan Glow", "#63ECF4", false),
        new("trail-blue-sparks", CosmeticKind.Trail, "Blue Sparks", "#4C8DFF", false),
        new("trail-cloud", CosmeticKind.Trail, "Cloud", "#D8ECF0", false),
        new("trail-sparks", CosmeticKind.Trail, "Sparks", "#F5A64A", false),
        new("trail-amber-bolts", CosmeticKind.Trail, "Amber Bolts", "#FFB43D", false),
        new("trail-coins", CosmeticKind.Trail, "Coins", "#E8C54E", false),
        new("trail-vortex", CosmeticKind.Trail, "Vortex", "#956BD4", false),
    };

    public static IReadOnlyList<CosmeticDefinition> All => Definitions;

    public static IEnumerable<CosmeticDefinition> OfKind(CosmeticKind kind)
    {
        return Definitions.Where(definition => definition.Kind == kind);
    }

    public static CosmeticDefinition? Find(CosmeticKind kind, string id)
    {
        return Definitions.FirstOrDefault(definition =>
            definition.Kind == kind && string.Equals(definition.Id, id, StringComparison.Ordinal));
    }

    public static CosmeticDefinition? FindById(string id)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, id, StringComparison.Ordinal));
    }

    public static HashSet<string> CreateBaseUnlockSet()
    {
        return Definitions
            .Where(definition => definition.UnlockedByDefault)
            .Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
