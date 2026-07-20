using Godot;
using Velocitex.Core.Profile;

namespace Velocitex.UI.Visual;

public partial class CosmeticSwatchButton : Button
{
    public CosmeticDefinition Definition { get; private set; } = null!;
    public bool IsLocked { get; private set; }
    public bool IsSelected { get; private set; }
    public string UnlockRequirement { get; private set; } = string.Empty;
    public Color PreviewPrimaryColor => _primaryColor;
    public Color PreviewSecondaryColor => _secondaryColor;

    private Color _primaryColor = new("d12b3f");
    private Color _secondaryColor = new("e8d9b8");

    public override void _Ready()
    {
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
    }

    public void Configure(
        CosmeticDefinition definition,
        bool locked,
        bool selected,
        string unlockRequirement,
        Color primaryColor,
        Color secondaryColor)
    {
        Definition = definition;
        IsLocked = locked;
        IsSelected = selected;
        UnlockRequirement = unlockRequirement;
        _primaryColor = primaryColor;
        _secondaryColor = secondaryColor;
        Text = string.Empty;
        CustomMinimumSize = new Vector2(50.0f, 50.0f);
        TooltipText = string.Empty;
        FocusMode = FocusModeEnum.All;
        ApplyStyles();
        QueueRedraw();
    }

    public void SetVisualState(bool selected, Color primaryColor, Color secondaryColor)
    {
        IsSelected = selected;
        _primaryColor = primaryColor;
        _secondaryColor = secondaryColor;
        ApplyStyles();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        float radius = Mathf.Min(Size.X, Size.Y) * 0.29f;
        switch (Definition.Kind)
        {
            case CosmeticKind.Color:
                DrawColorSample(center, radius);
                break;
            case CosmeticKind.Pattern:
                DrawPatternSample(center, radius);
                break;
            case CosmeticKind.Trail:
                DrawTrailSample(center, radius);
                break;
        }

        if (IsLocked)
        {
            DrawRect(new Rect2(4.0f, 4.0f, Size.X - 8.0f, Size.Y - 8.0f), new Color(0.025f, 0.035f, 0.045f, 0.54f), true);
            DrawLock(new Vector2(Size.X - 13.0f, 13.0f));
        }
    }

    private void ApplyStyles()
    {
        Color selectedBorder = new("f0b45f");
        Color normalBorder = IsLocked ? new Color("596168") : new Color("66747c");
        Color border = IsSelected ? selectedBorder : normalBorder;
        int borderWidth = IsSelected ? 3 : 1;
        AddThemeStyleboxOverride("normal", CreateStyle(new Color("172128"), border, borderWidth));
        AddThemeStyleboxOverride("hover", CreateStyle(new Color("233039"), IsLocked ? new Color("929ba0") : new Color("d5a05b"), 2));
        AddThemeStyleboxOverride("pressed", CreateStyle(new Color("10171d"), selectedBorder, 3));
        AddThemeStyleboxOverride("focus", CreateStyle(Colors.Transparent, selectedBorder, 2));
    }

    private static StyleBoxFlat CreateStyle(Color background, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
        };
    }

    private void DrawColorSample(Vector2 center, float radius)
    {
        Color color = new(Definition.PreviewValue);
        DrawCircle(center, radius, color);
        DrawArc(center, radius, 0.0f, Mathf.Tau, 32, new Color(1.0f, 1.0f, 1.0f, 0.48f), 1.5f, true);
        DrawCircle(center + new Vector2(-radius * 0.32f, -radius * 0.34f), radius * 0.18f, new Color(1.0f, 1.0f, 1.0f, 0.24f));
    }

    private void DrawPatternSample(Vector2 center, float radius)
    {
        DrawCircle(center, radius, _primaryColor);
        string pattern = Definition.PreviewValue;
        switch (pattern)
        {
            case "halves":
                DrawHalfCircle(center, radius, _secondaryColor);
                break;
            case "spiral":
                DrawSpiral(center, radius, _secondaryColor);
                break;
            case "rings":
                DrawOrbitalRings(center, radius, _secondaryColor);
                break;
            case "sugar-dots":
                foreach (Vector2 offset in new[] { new Vector2(-7, -6), new Vector2(5, -8), new Vector2(-2, 1), new Vector2(8, 5), new Vector2(-8, 8) })
                {
                    DrawCircle(center + offset, 2.4f, _secondaryColor);
                }
                break;
            case "stars":
                DrawStar(center + new Vector2(-5.5f, -4.0f), 5.2f, _secondaryColor);
                DrawStar(center + new Vector2(6.0f, 6.0f), 3.7f, _secondaryColor);
                break;
            case "lightning":
                DrawLightningPattern(center, radius, _secondaryColor);
                break;
            case "caramel-drips":
                DrawCaramelDrips(center, radius, _secondaryColor);
                break;
            case "waves":
                DrawWave(center, -4.0f, radius, _secondaryColor);
                DrawWave(center, 5.0f, radius, _secondaryColor);
                break;
            case "licorice-stripes":
                DrawLicoriceRopes(center, radius, _secondaryColor);
                break;
            case "target":
                DrawArc(center, radius * 0.72f, 0, Mathf.Tau, 28, _secondaryColor, 3.0f, true);
                DrawArc(center, radius * 0.37f, 0, Mathf.Tau, 24, _secondaryColor, 3.0f, true);
                DrawCircle(center, 2.0f, _secondaryColor);
                break;
            case "cracks":
                DrawCrack(center, _secondaryColor);
                break;
            case "pearl":
                DrawCircle(center, radius * 0.82f, new Color(0.92f, 0.96f, 1.0f, 0.34f));
                DrawCircle(center + new Vector2(-5.0f, -6.0f), radius * 0.27f, new Color(1.0f, 1.0f, 1.0f, 0.72f));
                break;
        }

        DrawArc(center, radius, 0.0f, Mathf.Tau, 32, new Color(1.0f, 1.0f, 1.0f, 0.5f), 1.4f, true);
    }

    private void DrawTrailSample(Vector2 center, float radius)
    {
        if (string.Equals(Definition.Id, "off", StringComparison.Ordinal))
        {
            Color muted = new("879197");
            DrawArc(center, radius * 0.78f, 0.0f, Mathf.Tau, 28, muted, 2.2f, true);
            DrawLine(center + new Vector2(-9, 9), center + new Vector2(9, -9), muted, 2.8f, true);
            return;
        }

        Color color = new(Definition.PreviewValue);
        DrawLine(center + new Vector2(-12, 5), center + new Vector2(8, -4), new Color(color.R, color.G, color.B, 0.52f), 3.0f, true);
        DrawCircle(center + new Vector2(-10, 5), 4.0f, new Color(color.R, color.G, color.B, 0.48f));
        DrawCircle(center + new Vector2(-1, 1), 5.2f, new Color(color.R, color.G, color.B, 0.74f));
        DrawCircle(center + new Vector2(9, -4), 6.5f, color);
        DrawArc(center + new Vector2(9, -4), 6.5f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.5f), 1.2f, true);
    }

    private static Vector2[] HalfCirclePoints(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[18];
        points[0] = center;
        for (int index = 0; index < 17; index++)
        {
            float angle = Mathf.Lerp(-Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, index / 16.0f);
            points[index + 1] = center + (Vector2.FromAngle(angle) * radius);
        }
        return points;
    }

    private void DrawHalfCircle(Vector2 center, float radius, Color color)
    {
        DrawColoredPolygon(HalfCirclePoints(center, radius), color);
        DrawLine(center + new Vector2(0, -radius), center + new Vector2(0, radius), new Color(1, 1, 1, 0.42f), 1.2f, true);
    }

    private void DrawSpiral(Vector2 center, float radius, Color color)
    {
        const float strokeWidth = 3.0f;
        float edgeRadius = radius - (strokeWidth * 0.5f);
        Vector2[] points = new Vector2[38];
        for (int index = 0; index < points.Length; index++)
        {
            float progress = index / (float)(points.Length - 1);
            points[index] = center + (Vector2.FromAngle(progress * Mathf.Tau * 2.15f) * Mathf.Lerp(0.8f, edgeRadius, progress));
        }
        DrawPolyline(points, color, strokeWidth, true);
    }

    private void DrawLightningPattern(Vector2 center, float radius, Color color)
    {
        Vector2[] points =
        {
            new(-1.0f, -13.0f), new(-8.0f, -1.0f), new(-2.2f, -1.0f),
            new(-5.2f, 13.0f), new(8.0f, -4.0f), new(2.1f, -4.0f),
        };
        float scale = (radius - 1.0f) / 15.3f;
        for (int index = 0; index < points.Length; index++)
        {
            points[index] = center + (points[index] * scale);
        }
        DrawColoredPolygon(points, color);
    }

    private void DrawCaramelDrips(Vector2 center, float radius, Color color)
    {
        const float capY = -7.5f;
        Vector2[] cap = new Vector2[20];
        cap[0] = center + new Vector2(0.0f, -radius + 0.6f);
        for (int index = 0; index < 19; index++)
        {
            float progress = index / 18.0f;
            float x = Mathf.Lerp(-radius + 0.8f, radius - 0.8f, progress);
            float curve = capY + (Mathf.Sin(progress * Mathf.Pi * 3.0f) * 1.1f);
            cap[index + 1] = center + new Vector2(x, curve);
        }
        DrawColoredPolygon(cap, color);

        foreach ((float x, float length, float width) in new[]
        {
            (-8.0f, 6.0f, 4.2f),
            (-0.5f, 11.0f, 5.0f),
            (8.5f, 4.5f, 3.8f),
        })
        {
            float bottomY = capY + length;
            DrawLine(center + new Vector2(x, capY - 0.4f), center + new Vector2(x, bottomY), color, width, true);
            DrawCircle(center + new Vector2(x, bottomY), width * 0.5f, color);
        }
    }

    private void DrawOrbitalRings(Vector2 center, float radius, Color color)
    {
        const float strokeWidth = 2.7f;
        foreach ((float y, float height) in new[] { (-6.5f, 3.1f), (0.0f, 4.8f), (6.5f, 3.1f) })
        {
            float halfWidth = ChordHalfWidth(radius, y, strokeWidth);
            Rect2 ellipse = new(center.X - halfWidth, center.Y + y - height, halfWidth * 2.0f, height * 2.0f);
            Vector2[] points = new Vector2[28];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = (index / (float)(points.Length - 1)) * Mathf.Tau;
                points[index] = ellipse.GetCenter() + new Vector2(Mathf.Cos(angle) * halfWidth, Mathf.Sin(angle) * height);
            }
            DrawPolyline(points, color, strokeWidth, true);
        }
    }

    private void DrawWave(Vector2 center, float yOffset, float radius, Color color)
    {
        const float strokeWidth = 2.3f;
        float safeRadius = radius - (strokeWidth * 0.5f);
        float halfWidth = Mathf.Sqrt(Math.Max(0.0f, (safeRadius * safeRadius) - (yOffset * yOffset)));
        Vector2[] points = new Vector2[20];
        for (int index = 0; index < points.Length; index++)
        {
            float progress = index / (float)(points.Length - 1);
            float x = Mathf.Lerp(-halfWidth, halfWidth, progress);
            float edgeTaper = Mathf.Sin(progress * Mathf.Pi);
            Vector2 localPoint = new(x, yOffset + (Mathf.Sin(progress * Mathf.Tau * 1.5f) * 2.2f * edgeTaper));
            if (localPoint.Length() > safeRadius)
            {
                localPoint = localPoint.Normalized() * safeRadius;
            }
            points[index] = center + localPoint;
        }
        DrawPolyline(points, color, strokeWidth, true);
    }

    private void DrawLicoriceRopes(Vector2 center, float radius, Color color)
    {
        const float ropeWidth = 5.4f;
        float safeRadius = radius - (ropeWidth * 0.5f);
        Vector2 direction = new Vector2(0.72f, 1.0f).Normalized();
        Vector2 perpendicular = new Vector2(1.0f, -0.72f).Normalized();
        Color shadow = color.Darkened(0.48f);
        foreach (float offset in new[] { -6.5f, 6.5f })
        {
            float halfLength = Mathf.Sqrt(Math.Max(0.0f, (safeRadius * safeRadius) - (offset * offset)));
            Vector2 lineCenter = center + (perpendicular * offset);
            Vector2 start = lineCenter - (direction * halfLength);
            Vector2 end = lineCenter + (direction * halfLength);
            DrawLine(start, end, shadow, ropeWidth, true);
            DrawLine(start + (perpendicular * 0.8f), end + (perpendicular * 0.8f), color.Lightened(0.22f), 1.35f, true);
            for (float amount = 0.2f; amount < 0.9f; amount += 0.22f)
            {
                Vector2 knot = start.Lerp(end, amount);
                DrawLine(knot - (perpendicular * 2.1f), knot + (perpendicular * 2.1f), color, 0.9f, true);
            }
        }
    }

    private static float ChordHalfWidth(float radius, float offset, float strokeWidth)
    {
        float safeRadius = Math.Max(0.0f, radius - (strokeWidth * 0.5f));
        return Mathf.Sqrt(Math.Max(0.0f, (safeRadius * safeRadius) - (offset * offset)));
    }

    private void DrawCrack(Vector2 center, Color color)
    {
        Vector2[] spine =
        {
            center + new Vector2(-3.0f, -13.0f),
            center + new Vector2(1.5f, -6.0f),
            center + new Vector2(-2.5f, 0.5f),
            center + new Vector2(2.0f, 6.0f),
            center + new Vector2(-0.5f, 13.0f),
        };
        DrawPolyline(spine, color, 2.15f, true);
        DrawPolyline(new[] { spine[1], center + new Vector2(8.5f, -10.0f), center + new Vector2(11.0f, -7.0f) }, color, 1.65f, true);
        DrawPolyline(new[] { spine[2], center + new Vector2(-9.5f, 4.0f), center + new Vector2(-11.5f, 8.5f) }, color, 1.65f, true);
        DrawPolyline(new[] { spine[3], center + new Vector2(9.5f, 9.0f) }, color, 1.65f, true);
    }

    private void DrawStar(Vector2 center, float radius, Color color)
    {
        Vector2[] points = new Vector2[10];
        for (int index = 0; index < points.Length; index++)
        {
            float pointRadius = index % 2 == 0 ? radius : radius * 0.43f;
            points[index] = center + (Vector2.FromAngle((-Mathf.Pi * 0.5f) + (index * Mathf.Pi / 5.0f)) * pointRadius);
        }
        DrawColoredPolygon(points, color);
    }

    private void DrawLock(Vector2 center)
    {
        Color lockColor = new("f4ead1");
        DrawArc(center + new Vector2(0, -2.5f), 4.2f, Mathf.Pi, Mathf.Tau, 12, lockColor, 2.0f, true);
        DrawRect(new Rect2(center.X - 5.5f, center.Y - 2.0f, 11.0f, 8.5f), lockColor, true);
        DrawCircle(center + new Vector2(0, 1.3f), 1.2f, new Color("273139"));
    }
}
