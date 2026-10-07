using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Launcher.Application;

namespace Launcher.App.Views;

/// <summary>
/// Graphe des communautés observées, dessiné nativement (ADR-007 : pas de page web).
///
/// Le tracé ne fait que **poser** ce qu'ECHOS publie (`/api/groups`) : un nœud par
/// communauté, un lien par membre — la relation « appartient au groupe » publiée telle
/// quelle. Le rayon d'un nœud est un facteur d'échelle d'affichage de la taille publiée,
/// de même nature qu'une mise à l'échelle d'axe ; aucune mesure n'est calculée ici
/// (ADR-003 : ECHOS calcule, le Launcher présente).
/// </summary>
public sealed class GroupsGraphControl : Control
{
    /// <summary>Propriété liable des communautés publiées par ECHOS.</summary>
    public static readonly StyledProperty<IReadOnlyList<EchosGroup>?> GroupsProperty =
        AvaloniaProperty.Register<GroupsGraphControl, IReadOnlyList<EchosGroup>?>(nameof(Groups));

    private static readonly Color[] Palette =
    [
        Color.Parse("#2d9bf0"),
        Color.Parse("#e0a25a"),
        Color.Parse("#2ecc71"),
        Color.Parse("#a04cf0"),
        Color.Parse("#e05252"),
        Color.Parse("#5cc4ff"),
    ];

    private static readonly IBrush LinkBrush = new SolidColorBrush(Color.Parse("#2f3542"));
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#9aa0aa"));
    private static readonly IBrush HintBrush = new SolidColorBrush(Color.Parse("#5f6875"));
    private static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#e8e6e1"));
    private static readonly Typeface LabelTypeface = new(new FontFamily("Inter, Segoe UI, Ubuntu, sans-serif"));

    /// <summary>Membres dessinés autour d'un nœud au-delà duquel le détail est écourté.</summary>
    private const int MaxDrawnMembers = 12;

    static GroupsGraphControl()
    {
        AffectsRender<GroupsGraphControl>(GroupsProperty);
    }

    /// <summary>Communautés à tracer ; vide = message d'absence, jamais un graphe inventé.</summary>
    public IReadOnlyList<EchosGroup>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
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

        var groups = Groups ?? [];
        if (groups.Count == 0)
        {
            DrawCentered(context, "aucune communauté observée pour ce run", HintBrush, width, height, 12);
            return;
        }

        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(groups.Count)));
        var rows = Math.Max(1, (int)Math.Ceiling(groups.Count / (double)columns));
        var cellWidth = width / columns;
        var cellHeight = height / rows;
        var maxSize = groups.Max(group => Math.Max(1, group.Size));

        for (var index = 0; index < groups.Count; index++)
        {
            var cellCenter = new Point(
                (cellWidth * (index % columns)) + (cellWidth / 2),
                (cellHeight * (index / columns)) + (cellHeight / 2));
            DrawGroup(
                context,
                groups[index],
                cellCenter,
                cellWidth,
                cellHeight,
                maxSize,
                Palette[index % Palette.Length]);
        }
    }

    private static void DrawGroup(
        DrawingContext context, EchosGroup group, Point center, double cellWidth, double cellHeight,
        int maxSize, Color color)
    {
        // Facteur d'échelle d'affichage : racine de la taille publiée, bornée à la cellule.
        var scaled = Math.Sqrt(Math.Max(1, group.Size) / (double)maxSize);
        var radius = 14 + (Math.Min(cellWidth, cellHeight) * 0.16 * scaled);
        var linkRadius = radius + 16;
        var members = group.Members;
        var linkPen = new Pen(LinkBrush, 1);
        var memberBrush = new SolidColorBrush(color);

        var drawn = Math.Min(members.Count, MaxDrawnMembers);
        for (var member = 0; member < drawn; member++)
        {
            var angle = (-Math.PI / 2) + ((2 * Math.PI * member) / Math.Max(1, drawn));
            var point = new Point(
                center.X + (linkRadius * Math.Cos(angle)),
                center.Y + (linkRadius * Math.Sin(angle)));
            context.DrawLine(linkPen, center, point);
            context.DrawEllipse(memberBrush, null, point, 3, 3);
        }

        var nodePen = new Pen(memberBrush, 1.6);
        var nodeFill = new SolidColorBrush(new Color(0x47, color.R, color.G, color.B));
        context.DrawEllipse(nodeFill, nodePen, center, radius, radius);

        var label = Truncate(group.Label, 10);
        var labelText = Measure(label, 11.5, TitleBrush);
        context.DrawText(labelText, new Point(center.X - (labelText.Width / 2), center.Y - (labelText.Height / 2)));

        var detail = $"{group.Size} m.";
        var detailText = Measure(detail, 10.5, MutedBrush);
        context.DrawText(detailText, new Point(center.X - (detailText.Width / 2), center.Y + radius + 4));
    }

    private static void DrawCentered(
        DrawingContext context, string text, IBrush brush, double width, double height, double fontSize)
    {
        var formatted = Measure(text, fontSize, brush);
        context.DrawText(formatted, new Point((width - formatted.Width) / 2, (height - formatted.Height) / 2));
    }

    private static FormattedText Measure(string text, double fontSize, IBrush brush) => new(
        text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, fontSize, brush);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
