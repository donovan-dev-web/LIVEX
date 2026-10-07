using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Launcher.Application;

namespace Launcher.App.Views;

/// <summary>
/// Graphe de confiance entre entités, dessiné nativement (ADR-007 : pas de page web).
///
/// Chaque nœud est une entité observée, placée à sa **position publiée** ; chaque arête
/// est la relation `trust` publiée par l'entité source, son épaisseur et son opacité
/// variant avec le niveau de confiance publié — une mise à l'échelle d'affichage, pas un
/// agrégat (ADR-003 : ECHOS calcule, le Launcher présente).
/// </summary>
public sealed class TrustGraphControl : Control
{
    /// <summary>Propriété liable des nœuds observés.</summary>
    public static readonly StyledProperty<IReadOnlyList<EchosTrustNode>?> NodesProperty =
        AvaloniaProperty.Register<TrustGraphControl, IReadOnlyList<EchosTrustNode>?>(nameof(Nodes));

    /// <summary>Propriété liable des relations observées.</summary>
    public static readonly StyledProperty<IReadOnlyList<EchosTrustEdge>?> EdgesProperty =
        AvaloniaProperty.Register<TrustGraphControl, IReadOnlyList<EchosTrustEdge>?>(nameof(Edges));

    private static readonly Color Backdrop = Color.Parse("#080b10");
    private static readonly Color NodeFallback = Color.Parse("#5cc4ff");
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#5f6875"));
    private static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#e8e6e1"));
    private static readonly Typeface LabelTypeface = new(new FontFamily("Inter, Segoe UI, Ubuntu, sans-serif"));

    private static readonly Color[] GroupPalette =
    [
        Color.Parse("#2d9bf0"),
        Color.Parse("#e0a25a"),
        Color.Parse("#2ecc71"),
        Color.Parse("#a04cf0"),
        Color.Parse("#e05252"),
        Color.Parse("#5cc4ff"),
        Color.Parse("#f0e05a"),
        Color.Parse("#7ad1a0"),
    ];

    static TrustGraphControl()
    {
        AffectsRender<TrustGraphControl>(NodesProperty, EdgesProperty);
    }

    /// <summary>Entités du graphe.</summary>
    public IReadOnlyList<EchosTrustNode>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <summary>Relations de confiance observées.</summary>
    public IReadOnlyList<EchosTrustEdge>? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 40 || height < 40)
        {
            return;
        }

        context.FillRectangle(new SolidColorBrush(Backdrop), new Rect(0, 0, width, height));

        var nodes = Nodes ?? [];
        var edges = Edges ?? [];
        if (nodes.Count == 0)
        {
            DrawCentered(context, "aucune entité observée pour ce run", width, height, MutedBrush, 12);
            return;
        }

        var points = Layout(nodes, width, height);
        var byId = new Dictionary<string, Point>(StringComparer.Ordinal);
        var radiusByGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            byId[nodes[index].Id] = points[index];
            if (!string.IsNullOrEmpty(nodes[index].Group) && !radiusByGroup.ContainsKey(nodes[index].Group!))
            {
                radiusByGroup[nodes[index].Group!] = radiusByGroup.Count;
            }
        }

        foreach (var edge in edges)
        {
            if (!byId.TryGetValue(edge.Source, out var source) || !byId.TryGetValue(edge.Target, out var target))
            {
                continue;
            }

            var weight = Math.Clamp(edge.Weight ?? 0.5, 0, 1);
            var pen = new Pen(
                new SolidColorBrush(TrustColor(weight)),
                1 + (4 * weight));
            context.DrawLine(pen, source, target);
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var color = string.IsNullOrEmpty(node.Group)
                ? NodeFallback
                : GroupPalette[radiusByGroup[node.Group!] % GroupPalette.Length];
            var center = points[index];
            context.DrawEllipse(
                new SolidColorBrush(color),
                new Pen(new SolidColorBrush(Color.Parse("#0b0f16")), 1.2),
                center,
                7,
                7);
            var label = new FormattedText(
                node.Id, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, 10.5, TitleBrush);
            context.DrawText(label, new Point(center.X + 9, center.Y - (label.Height / 2)));
        }

        DrawLegend(context, width, height);
    }

    /// <summary>Couleur d'une relation : confiance 0 (rouge) → 0,5 (or) → 1 (vert).
    /// Mise en forme d'affichage d'une valeur publiée, jamais un calcul (ADR-003).</summary>
    private static Color TrustColor(double weight)
    {
        static Color Mix(Color from, Color to, double t) => Color.FromRgb(
            (byte)(from.R + ((to.R - from.R) * t)),
            (byte)(from.G + ((to.G - from.G) * t)),
            (byte)(from.B + ((to.B - from.B) * t)));

        var low = Color.Parse("#e05252");
        var mid = Color.Parse("#e0a25a");
        var high = Color.Parse("#2ecc71");
        return weight <= 0.5 ? Mix(low, mid, weight * 2) : Mix(mid, high, (weight - 0.5) * 2);
    }

    /// <summary>Légende : l'échelle de couleur/l'épaisseur utilisée pour les relations.</summary>
    private static void DrawLegend(DrawingContext context, double width, double height)
    {
        const double swatchWidth = 46;
        const double y = 14;
        var x = width - (swatchWidth * 3) - 118;
        if (x < 8)
        {
            return;
        }

        var caption = new FormattedText(
            "confiance publiée :",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            10.5,
            MutedBrush);
        context.DrawText(caption, new Point(x - caption.Width - 8, y + (caption.Height / 2) - 6));

        var stops = new[] { ("0", 0d), ("0,5", 0.5d), ("1", 1d) };
        for (var index = 0; index < stops.Length; index++)
        {
            var left = x + (index * swatchWidth);
            var pen = new Pen(new SolidColorBrush(TrustColor(stops[index].Item2)), 1 + (4 * stops[index].Item2));
            context.DrawLine(pen, new Point(left, y + 8), new Point(left + (swatchWidth - 14), y + 8));
            var tick = new FormattedText(
                stops[index].Item1,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                10,
                MutedBrush);
            context.DrawText(tick, new Point(left + (swatchWidth - 12), y + 8 - (tick.Height / 2)));
        }
    }

    /// <summary>
    /// Position d'affichage : coordonnées publiées si présentes, sinon répartition
    /// circulaire — aucune simulation de forces, qui serait un calcul local.
    /// </summary>
    private static Point[] Layout(IReadOnlyList<EchosTrustNode> nodes, double width, double height)
    {
        var points = new Point[nodes.Count];
        var hasPositions = nodes.All(node => node.X is not null && node.Y is not null);
        const double padding = 26;
        if (hasPositions)
        {
            var extentX = Math.Max(1, nodes.Max(node => node.X!.Value));
            var extentY = Math.Max(1, nodes.Max(node => node.Y!.Value));
            var scaleX = (width - (padding * 2)) / extentX;
            var scaleY = (height - (padding * 2)) / extentY;
            for (var index = 0; index < nodes.Count; index++)
            {
                points[index] = new Point(
                    padding + (nodes[index].X!.Value * scaleX),
                    padding + (nodes[index].Y!.Value * scaleY));
            }

            return points;
        }

        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(width, height) / 2 - padding;
        for (var index = 0; index < nodes.Count; index++)
        {
            var angle = (-Math.PI / 2) + ((2 * Math.PI * index) / nodes.Count);
            points[index] = new Point(
                centerX + (radius * Math.Cos(angle)),
                centerY + (radius * Math.Sin(angle)));
        }

        return points;
    }

    private static void DrawCentered(
        DrawingContext context, string text, double width, double height, IBrush brush, double fontSize)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, fontSize, brush);
        context.DrawText(formatted, new Point((width - formatted.Width) / 2, (height - formatted.Height) / 2));
    }
}
