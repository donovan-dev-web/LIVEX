using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Launcher.Application;

namespace Launcher.App.Views;

/// <summary>
/// Vue 2D du monde observé, dessinée nativement (ADR-007 : pas de page web).
///
/// Le contrôle ne fait que **projeter** ce qu'ECHOS publie : description de monde
/// (cellules de terrain, obstacles, ressources initiales, régions), réserves du tick
/// et entités avec leur position. Les couleurs par groupe et le rayon des points sont
/// des choix de rendu ; aucune mesure n'est calculée ici (ADR-003).
///
/// <para>
/// Cadrage (refonte) : **une seule unité par couche**, jamais mélangée. La description
/// publie ses dimensions et sa taille de cellule en unités monde ; cellules, régions et
/// ressources initiales sont publiées en **indices de grille** et converties par la
/// taille de cellule ; obstacles, réserves du tick et entités sont déjà en unités
/// monde. L'échelle est **uniforme** (le monde n'est pas étiré) et le cadre est centré
/// dans le contrôle.
/// </para>
/// </summary>
public sealed class WorldViewControl : Control
{
    /// <summary>Description statique du monde (absente = observation seule).</summary>
    public static readonly StyledProperty<EchosWorldDescription?> DescriptionProperty =
        AvaloniaProperty.Register<WorldViewControl, EchosWorldDescription?>(nameof(Description));

    /// <summary>Entités observées au tick affiché.</summary>
    public static readonly StyledProperty<IReadOnlyList<EchosAgentSnapshot>?> AgentsProperty =
        AvaloniaProperty.Register<WorldViewControl, IReadOnlyList<EchosAgentSnapshot>?>(nameof(Agents));

    /// <summary>Réserves observées au tick affiché.</summary>
    public static readonly StyledProperty<IReadOnlyList<EchosTickResource>?> TickResourcesProperty =
        AvaloniaProperty.Register<WorldViewControl, IReadOnlyList<EchosTickResource>?>(nameof(TickResources));

    private static readonly Color Backdrop = Color.Parse("#080b10");
    private static readonly Color PlotBackdrop = Color.Parse("#0a0e15");
    private static readonly Color Walkable = Color.Parse("#131a26");
    private static readonly Color Unwalkable = Color.Parse("#241b1b");
    private static readonly Color GridLine = Color.Parse("#1b2331");
    private static readonly Color GridLineMajor = Color.Parse("#2a3547");
    private static readonly Color RegionTint = Color.Parse("#1d2733");
    private static readonly Color ObstacleColor = Color.Parse("#6b7280");
    private static readonly Color ResourceColor = Color.Parse("#2ecc71");
    private static readonly Color TickResourceColor = Color.Parse("#e0a25a");
    private static readonly Color AgentFallback = Color.Parse("#5cc4ff");
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#5f6875"));
    private static readonly Typeface LabelTypeface = new(new FontFamily("Inter, Segoe UI, Ubuntu, sans-serif"));

    private const double Padding = 14;

    /// <summary>Durée de la transition d'affichage entre deux snapshots publiés (ms).
    /// Les positions sources et cibles sont publiées par ECHOS : seule la**
    /// progression à l'écran est continues, pour qu'une entité glisse au lieu de
    /// teleporter d'un relevé au suivant (choix de rendu, aucune donnée inventée).</summary>
    private const int MotionDurationMs = 450;

    private readonly Dictionary<string, Point> _motionFrom = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Point> _motionTo = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _motionTimer;
    private DateTimeOffset _motionStart = DateTimeOffset.MinValue;

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

    static WorldViewControl()
    {
        AffectsRender<WorldViewControl>(DescriptionProperty, AgentsProperty, TickResourcesProperty);
        AgentsProperty.Changed.AddClassHandler<WorldViewControl>((control, _) => control.OnAgentsChanged());
    }

    /// <summary>Initialise le contrôle et son horloge de transition entre snapshots.</summary>
    public WorldViewControl()
    {
        _motionTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(33),
            DispatcherPriority.Background,
            (sender, _) =>
            {
                if ((DateTimeOffset.UtcNow - _motionStart).TotalMilliseconds >= MotionDurationMs)
                {
                    (sender as DispatcherTimer)?.Stop();
                }

                InvalidateVisual();
            });
    }

    /// <summary>Nouveaux snapshots publiés : la transition repart de la position
    /// précédemment affichée vers la nouvelle, jamais l'inverse.</summary>
    private void OnAgentsChanged()
    {
        var current = Agents ?? [];
        _motionFrom.Clear();
        foreach (var (id, position) in _motionTo)
        {
            _motionFrom[id] = position;
        }

        _motionTo.Clear();
        foreach (var agent in current)
        {
            _motionTo[agent.Id] = new Point(agent.X, agent.Y);
        }

        if (_motionFrom.Count == 0 || _motionTo.Count == 0)
        {
            _motionTimer.Stop();
            return;
        }

        _motionStart = DateTimeOffset.UtcNow;
        _motionTimer.Start();
    }

    /// <summary>Position d'affichage d'une entité, interpolée entre le dernier
    /// snapshot affiché et le nouveau pendant la transition (rendu seul).</summary>
    private Point MotionPoint(EchosAgentSnapshot agent)
    {
        var target = new Point(agent.X, agent.Y);
        var elapsed = (DateTimeOffset.UtcNow - _motionStart).TotalMilliseconds;
        if (_motionFrom.Count == 0 || elapsed >= MotionDurationMs)
        {
            return target;
        }

        if (!_motionFrom.TryGetValue(agent.Id, out var origin))
        {
            return target; // entité apparue : aucune origine publiée à interpoler
        }

        var t = Math.Clamp(elapsed / MotionDurationMs, 0, 1);
        return new Point(
            origin.X + ((target.X - origin.X) * t),
            origin.Y + ((target.Y - origin.Y) * t));
    }

    /// <summary>Description statique du monde.</summary>
    public EchosWorldDescription? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Entités observées.</summary>
    public IReadOnlyList<EchosAgentSnapshot>? Agents
    {
        get => GetValue(AgentsProperty);
        set => SetValue(AgentsProperty, value);
    }

    /// <summary>Réserves observées.</summary>
    public IReadOnlyList<EchosTickResource>? TickResources
    {
        get => GetValue(TickResourcesProperty);
        set => SetValue(TickResourcesProperty, value);
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

        var description = Description;
        var agents = Agents ?? [];
        var resources = TickResources ?? [];
        if (description is null && agents.Count == 0 && resources.Count == 0)
        {
            DrawCentered(context, "aucune observation de monde pour ce tick", width, height);
            return;
        }

        // Échelle de la grille : indices de grille → unités monde.
        var columns = description is { Cells.Count: > 0 }
            ? description.Cells.Max(cell => cell.X) + 1
            : 0;
        var rows = description is { Cells.Count: > 0 }
            ? description.Cells.Max(cell => cell.Y) + 1
            : 0;
        var cellSize = description is { CellSize: > 0 } ? description.CellSize : 0d;
        if (cellSize <= 0 && columns > 0 && description is { Width: > 0 })
        {
            cellSize = (double)description.Width / columns;
        }

        if (cellSize <= 0)
        {
            cellSize = 1;
        }

        // Étendue en unités monde : dimensions publiées, sinon la grille, sinon les
        // positions observées — cadrage d'affichage, jamais une mesure du monde.
        var extentX = description is { Width: > 0 } ? (double)description.Width : 0d;
        var extentY = description is { Height: > 0 } ? (double)description.Height : 0d;
        if (extentX <= 0 && columns > 0)
        {
            extentX = columns * cellSize;
        }

        if (extentY <= 0 && rows > 0)
        {
            extentY = rows * cellSize;
        }

        foreach (var agent in agents)
        {
            extentX = Math.Max(extentX, agent.X);
            extentY = Math.Max(extentY, agent.Y);
        }

        foreach (var resource in resources)
        {
            extentX = Math.Max(extentX, resource.X);
            extentY = Math.Max(extentY, resource.Y);
        }

        extentX = Math.Max(1, extentX);
        extentY = Math.Max(1, extentY);

        // Échelle uniforme (monde non déformé), cadre centré dans le contrôle.
        var availableWidth = Math.Max(1, width - (Padding * 2));
        var availableHeight = Math.Max(1, height - (Padding * 2));
        var scale = Math.Max(0.01, Math.Min(availableWidth / extentX, availableHeight / extentY));
        var plotWidth = extentX * scale;
        var plotHeight = extentY * scale;
        var originX = (width - plotWidth) / 2;
        var originY = (height - plotHeight) / 2;
        Point Map(double x, double y) => new(originX + (x * scale), originY + (y * scale));

        var plotRect = new Rect(originX, originY, plotWidth, plotHeight);
        context.DrawRectangle(new SolidColorBrush(PlotBackdrop), null, plotRect);

        // Découpe au cadre publié : une géométrie hors étendue (donnée hétérogène)
        // reste dans le cadre au lieu de déborder sur le panneau.
        using (context.PushClip(plotRect))
        {
            if (description is not null)
            {
                DrawTerrain(context, description, Map);
                DrawRegions(context, description, Map, cellSize);
                DrawGrid(context, Map, cellSize, extentX, extentY);
                DrawStaticResources(context, description, Map, cellSize);
                DrawObstacles(context, description, Map, scale);
            }

            foreach (var resource in resources)
            {
                var point = Map(resource.X, resource.Y);
                context.DrawRectangle(
                    new SolidColorBrush(TickResourceColor),
                    null,
                    new Rect(point.X - 3.5, point.Y - 3.5, 7, 7));
            }

            var groupIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var agent in agents)
            {
                var brush = new SolidColorBrush(GroupColor(groupIndex, agent.Group));
                var displayed = MotionPoint(agent);
                var point = Map(displayed.X, displayed.Y);
                context.DrawEllipse(brush, null, point, 4, 4);
            }
        }

        // Cadre du monde : rappel visuel de l'étendue publiée.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#262b34")), 1), plotRect);
    }

    /// <summary>Couleur d'affichage d'une communauté (index de découverte, aucun calcul).</summary>
    private static Color GroupColor(Dictionary<string, int> index, string? group)
    {
        if (string.IsNullOrEmpty(group))
        {
            return AgentFallback;
        }

        if (!index.TryGetValue(group, out var position))
        {
            position = index.Count;
            index[group] = position;
        }

        return GroupPalette[position % GroupPalette.Length];
    }

    /// <summary>
    /// Terrain : une cellule par coordonnée grille, convertie en unités monde
    /// (indice × taille de cellule). Le maillage lui-même est tracé par <see cref="DrawGrid"/>.
    /// </summary>
    private static void DrawTerrain(
        DrawingContext context,
        EchosWorldDescription description,
        Func<double, double, Point> map)
    {
        var cellSize = CellSizeOf(description);
        foreach (var cell in description.Cells)
        {
            var start = map(cell.X * cellSize, cell.Y * cellSize);
            var end = map((cell.X + 1) * cellSize, (cell.Y + 1) * cellSize);
            context.FillRectangle(
                new SolidColorBrush(cell.Walkable ? Walkable : Unwalkable),
                new Rect(start, end));
        }
    }

    /// <summary>Taille de cellule publiée, avec repli sur étendue ÷ colonnes.</summary>
    private static double CellSizeOf(EchosWorldDescription description)
    {
        if (description.CellSize > 0)
        {
            return description.CellSize;
        }

        var columns = description.Cells.Count == 0 ? 0 : description.Cells.Max(cell => cell.X) + 1;
        return columns > 0 && description.Width > 0 ? (double)description.Width / columns : 1;
    }

    /// <summary>
    /// Grille du monde : le maillage complet publié (colonnes × lignes de cellules)
    /// sur **toute** l'étendue du monde — pas seulement la zone couverte par les
    /// cellules de terrain, et pas les bords des régions. Quand deux filets sont
    /// trop serrés pour être lus, un filet sur N est tracé : la grille reste
    /// visible à toute échelle.
    /// </summary>
    private static void DrawGrid(
        DrawingContext context,
        Func<double, double, Point> map,
        double cellSize,
        double extentX,
        double extentY)
    {
        if (cellSize <= 0)
        {
            return;
        }

        var columns = Math.Max(1, (int)Math.Round(extentX / cellSize));
        var rows = Math.Max(1, (int)Math.Round(extentY / cellSize));
        var topLeft = map(0, 0);
        var bottomRight = map(columns * cellSize, rows * cellSize);
        var stepX = Math.Abs(bottomRight.X - topLeft.X) / columns;
        if (stepX <= 0)
        {
            return;
        }

        var stride = Math.Max(1, (int)Math.Ceiling(3 / stepX));
        var gridPen = new Pen(new SolidColorBrush(GridLine), 1);
        var majorPen = new Pen(new SolidColorBrush(GridLineMajor), 1);
        for (var column = 0; column <= columns; column++)
        {
            if (column % stride != 0 && column != columns)
            {
                continue;
            }

            var x = topLeft.X + (column * stepX);
            context.DrawLine(
                column % (stride * 5) == 0 ? majorPen : gridPen,
                new Point(x, topLeft.Y),
                new Point(x, bottomRight.Y));
        }

        var stepY = Math.Abs(bottomRight.Y - topLeft.Y) / rows;
        if (stepY <= 0)
        {
            return;
        }

        var rowStride = Math.Max(1, (int)Math.Ceiling(3 / stepY));
        for (var row = 0; row <= rows; row++)
        {
            if (row % rowStride != 0 && row != rows)
            {
                continue;
            }

            var y = topLeft.Y + (row * stepY);
            context.DrawLine(
                row % (rowStride * 5) == 0 ? majorPen : gridPen,
                new Point(topLeft.X, y),
                new Point(bottomRight.X, y));
        }
    }

    /// <summary>Régions : coordonnées grille → unités monde (indice × taille de cellule).
    /// Teinte translucide : la grille des cellules et le terrain restent visibles
    /// dessous — une région opaque masquait le maillage du monde.</summary>
    private static void DrawRegions(DrawingContext context, EchosWorldDescription description,
        Func<double, double, Point> map, double cellSize)
    {
        var brush = new SolidColorBrush(Color.FromArgb(90, RegionTint.R, RegionTint.G, RegionTint.B));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0x2d, 0x9b, 0xf0)), 1);
        foreach (var region in description.Regions)
        {
            var start = map(region.X * cellSize, region.Y * cellSize);
            var end = map((region.X + region.Width) * cellSize, (region.Y + region.Height) * cellSize);
            var rect = new Rect(start, end);
            context.DrawRectangle(brush, pen, rect);
        }
    }

    /// <summary>Ressources initiales : au centre de leur cellule (indices de grille).</summary>
    private static void DrawStaticResources(DrawingContext context, EchosWorldDescription description,
        Func<double, double, Point> map, double cellSize)
    {
        var brush = new SolidColorBrush(ResourceColor);
        foreach (var resource in description.Resources)
        {
            var point = MapDiamond(
                map((resource.X + 0.5) * cellSize, (resource.Y + 0.5) * cellSize),
                4);
            context.DrawGeometry(brush, null, point);
        }
    }

    /// <summary>Obstacles : géométrie publiée en unités monde (rayon mis à l'échelle).</summary>
    private static void DrawObstacles(DrawingContext context, EchosWorldDescription description,
        Func<double, double, Point> map, double scale)
    {
        var brush = new SolidColorBrush(ObstacleColor);
        var pen = new Pen(new SolidColorBrush(Color.Parse("#9aa0aa")), 1);
        foreach (var obstacle in description.Obstacles)
        {
            var radius = Math.Max(2, obstacle.Radius * scale);
            context.DrawEllipse(brush, pen, map(obstacle.X, obstacle.Y), radius, radius);
        }
    }

    private static StreamGeometry MapDiamond(Point center, double half)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center.X, center.Y - half), isFilled: true);
            context.LineTo(new Point(center.X + half, center.Y));
            context.LineTo(new Point(center.X, center.Y + half));
            context.LineTo(new Point(center.X - half, center.Y));
            context.EndFigure(isClosed: true);
        }

        return geometry;
    }

    private static void DrawCentered(DrawingContext context, string text, double width, double height)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, 12, MutedBrush);
        context.DrawText(formatted, new Point((width - formatted.Width) / 2, (height - formatted.Height) / 2));
    }
}
