using Godot;
using Velocitex.Core.Save;

namespace Velocitex.Core.Profile;

public static class CandyVisualStyle
{
    private static readonly IReadOnlyDictionary<string, int> PatternModes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["none"] = 0,
        ["halves"] = 1,
        ["spiral"] = 2,
        ["rings"] = 3,
        ["sugar-dots"] = 4,
        ["stars"] = 5,
        ["lightning"] = 6,
        ["caramel-drips"] = 7,
        ["waves"] = 8,
        ["licorice-stripes"] = 9,
        ["target"] = 10,
        ["cracks"] = 11,
        ["pearl"] = 12,
        ["inspection-grid"] = 13,
    };

    public static void ApplyCandyMaterial(ShaderMaterial material, PlayerProfile profile)
    {
        material.SetShaderParameter("primary_color", ResolveColor(CosmeticKind.Color, profile.PrimaryColorId, "cherry"));
        material.SetShaderParameter("secondary_color", ResolveColor(CosmeticKind.Color, profile.SecondaryColorId, "vanilla"));
        material.SetShaderParameter(
            "pattern_mode",
            PatternModes.TryGetValue(profile.PatternId, out int mode) ? mode : 0);
        material.SetShaderParameter("finish_mode", profile.FinishId switch
        {
            "glossy" => 1,
            "metal" => 2,
            "frosted" => 3,
            "candy-glaze" => 4,
            "sparkling" => 5,
            _ => 0,
        });
    }

    public static Color ResolveTrailColor(string trailId)
    {
        return ResolveColor(CosmeticKind.Trail, trailId, "off");
    }

    public static int ResolvePatternMode(string patternId) => PatternModes.TryGetValue(patternId, out int mode) ? mode : 0;

    private static Color ResolveColor(CosmeticKind kind, string id, string fallbackId)
    {
        CosmeticDefinition definition = CosmeticCatalog.Find(kind, id)
            ?? CosmeticCatalog.Find(kind, fallbackId)!;
        return new Color(definition.PreviewValue);
    }
}
