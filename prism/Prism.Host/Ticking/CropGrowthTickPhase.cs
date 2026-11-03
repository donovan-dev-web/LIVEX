using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class CropGrowthTickPhase(
    WorldGrid? world,
    EventWheel eventWheel,
    ulong runSeed,
    WorldCalendar calendar,
    IReadOnlyCollection<CropGrowthRule> rules) : IOptionalCheckpointableWorldTickPhaseHandler
{
    public WorldTickPhase Phase => WorldTickPhase.ScheduledUpdates;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (world is null || rules.Count == 0)
        {
            _ = eventWheel.AdvanceTo(context.Tick);
            return;
        }

        var season = calendar.GetSnapshot(context.Tick).Season;
        foreach (var rule in rules)
        {
            CropGrowthSystem.ResumeGrowingCrops(world, eventWheel, runSeed, context.Tick, season, rule);
        }

        var dueEvents = eventWheel.AdvanceTo(context.Tick);
        foreach (var rule in rules)
        {
            foreach (var growthEvent in CropGrowthSystem.ProcessDueEvents(
                         world,
                         eventWheel,
                         runSeed,
                         context.Tick,
                         season,
                         rule,
                         dueEvents))
            {
                var kind = growthEvent.Kind == CropGrowthEventKind.Ripe ? "crop_ripe" : "crop_withered";
                var payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        position = growthEvent.Target,
                        stage = growthEvent.Stage,
                    },
                    ProtocolJson.Options);
                context.AddEvent(new EventEnvelope(
                    ProtocolVersion.Current.ToString(),
                    context.Tick,
                    $"{kind}:{rule.CropMaterialId}:{growthEvent.Target.X}:{growthEvent.Target.Y}:{growthEvent.Target.Z}:{context.Tick}",
                    kind,
                    null,
                    payload));
            }
        }
    }

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(eventWheel.CaptureState(), ProtocolJson.Options);

    public void RestoreEmptyState()
    {
        if (eventWheel.CurrentTick != 0 || eventWheel.Count != 0)
        {
            throw new JsonException("Growth state can only be initialized as empty in a new event wheel.");
        }
    }

    public void RestoreState(JsonElement state)
    {
        var checkpoint = state.Deserialize<byte[]>(ProtocolJson.Options)
                        ?? throw new JsonException("The growth checkpoint is empty.");
        eventWheel.RestoreState(checkpoint);
    }
}

public sealed class CalendarEventTickPhase(
    WorldCalendar calendar) : IWorldTickPhaseHandler
{
    public WorldTickPhase Phase => WorldTickPhase.Calendar;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = calendar.GetSnapshot(context.Tick);
        if (context.Tick == 0)
        {
            return;
        }

        var previous = calendar.GetSnapshot(context.Tick - 1);
        if (previous.Day != current.Day)
        {
            AddEvent(context, "day_changed", new { current.Day, current.Season });
        }

        if (previous.Season != current.Season)
        {
            AddEvent(context, "season_changed", new { current.Day, current.Season });
        }
    }

    private static void AddEvent(WorldTickContext context, string kind, object payload)
    {
        var json = JsonSerializer.SerializeToElement(payload, ProtocolJson.Options);
        context.AddEvent(new EventEnvelope(
            ProtocolVersion.Current.ToString(),
            context.Tick,
            $"{kind}:{context.Tick}",
            kind,
            null,
            json));
    }
}
