using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

const int width = 256;
const int height = 256;
const int regionSize = 16;
const int regionCountX = width / regionSize;
const int regionCountY = height / regionSize;
const int regionCount = regionCountX * regionCountY;
const int agentCount = 1_000;
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var heights = new byte[width * height];
var blocked = new bool[width * height];
var features = new byte[width * height];
GenerateRegion(0, width, height, regionSize, regionCountX,
    new byte[width * height], new bool[width * height], new byte[width * height]);
var generation = Stopwatch.StartNew();

for (var region = 0; region < regionCount; region++)
{
    GenerateRegion(region, width, height, regionSize, regionCountX, heights, blocked, features);
}

generation.Stop();
Console.WriteLine($"B6 prototype 2.5D generation: {generation.Elapsed.TotalMilliseconds:F2} ms, {width * height:N0} columns, {regionCount:N0} regions");
var warmupHeights = new byte[width * height];
var warmupBlocked = new bool[width * height];
var warmupFeatures = new byte[width * height];
Parallel.For(0, regionCount,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    region => GenerateRegion(region, width, height, regionSize, regionCountX,
        warmupHeights, warmupBlocked, warmupFeatures));
var generationParallelSamples = new double[3];
for (var sample = 0; sample < generationParallelSamples.Length; sample++)
{
    var parallelHeights = new byte[width * height];
    var parallelBlocked = new bool[width * height];
    var parallelFeatures = new byte[width * height];
    var parallelTimer = Stopwatch.StartNew();
    Parallel.For(0, regionCount,
        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
        region => GenerateRegion(region, width, height, regionSize, regionCountX,
            parallelHeights, parallelBlocked, parallelFeatures));
    parallelTimer.Stop();
    if (!heights.AsSpan().SequenceEqual(parallelHeights) ||
        !blocked.AsSpan().SequenceEqual(parallelBlocked) ||
        !features.AsSpan().SequenceEqual(parallelFeatures))
    {
        throw new InvalidOperationException("Parallel regional generation changed deterministic world data.");
    }

    generationParallelSamples[sample] = parallelTimer.Elapsed.TotalMilliseconds;
}

Console.WriteLine($"B6 parallel regional generation ({Environment.ProcessorCount} max workers): median={MedianMilliseconds(generationParallelSamples):F2} ms, deterministic equality=verified");
var slopeTransitions = MeasureCardinalSlopeTransitions(width, height, heights, blocked);
Console.WriteLine($"B1 slope transitions: {slopeTransitions.Pairs:N0} open cardinal edges, {slopeTransitions.PermittedDirections:N0} permitted directed transitions, {slopeTransitions.RejectedDirections:N0} rejected");

var pathfinder = new SurfacePathfinder(width, height, heights, blocked);
for (var agent = 0; agent < 32; agent++)
{
    var key = Mix64((ulong)agent + 0x7061746866696e64UL);
    var start = FindOpenCell((int)(key % (ulong)heights.Length), blocked);
    var target = FindOpenCell((int)(Mix64(key) % (ulong)heights.Length), blocked);
    pathfinder.TryFindPath(start, target, out _);
}

MeasureParallelPaths(32, width, height, heights, blocked);

foreach (var batchAgentCount in new[] { 100, agentCount })
{
    var pathTimer = Stopwatch.StartNew();
    var routeFailures = 0;
    var expandedTotal = 0L;

    for (var agent = 0; agent < batchAgentCount; agent++)
    {
        var key = Mix64((ulong)agent + 0x7061746866696e64UL);
        var start = FindOpenCell((int)(key % (ulong)heights.Length), blocked);
        var target = FindOpenCell((int)(Mix64(key) % (ulong)heights.Length), blocked);
        if (!pathfinder.TryFindPath(start, target, out var expanded))
        {
            routeFailures++;
        }

        expandedTotal += expanded;
    }

    pathTimer.Stop();
    Console.WriteLine($"B1 2.5D A* sequential: {batchAgentCount:N0}-agent path batch, {pathTimer.Elapsed.TotalMilliseconds:F2} ms, {routeFailures} failures, {expandedTotal:N0} expanded cells");
    var parallelResult = MeasureParallelPaths(batchAgentCount, width, height, heights, blocked);
    Console.WriteLine($"B1 2.5D A* parallel (max degree {Environment.ProcessorCount}): {batchAgentCount:N0}-agent path batch, {parallelResult.Elapsed.TotalMilliseconds:F2} ms, {parallelResult.Failures} failures, {parallelResult.Expanded:N0} expanded cells");
}

var regionTagMasks = new uint[regionCount];
for (var region = 0; region < regionCount; region++)
{
    regionTagMasks[region] = GetRegionTagMask(region, width, regionSize, features);
}

var agentPositions = new AgentPosition[agentCount];
for (var agent = 0; agent < agentCount; agent++)
{
    var key = Mix64((ulong)agent + 0x70657263657074UL);
    agentPositions[agent] = new AgentPosition(agent, (short)(key % width), (short)(Mix64(key) % height));
}

var observations = new AgentObservation[agentCount];
var perceptionTimer = Stopwatch.StartNew();
var perceivedFeatureTotal = 0L;
var perceivedRegionTotal = 0L;
for (var agent = 0; agent < agentCount; agent++)
{
    var position = agentPositions[agent];
    var x = (int)position.X;
    var y = (int)position.Y;
    var nearMinX = Math.Max(0, x - 16);
    var nearMaxX = Math.Min(width - 1, x + 16);
    var nearMinY = Math.Max(0, y - 16);
    var nearMaxY = Math.Min(height - 1, y + 16);
    var nearFeatures = new List<PerceivedFeature>();
    for (var row = nearMinY; row <= nearMaxY; row++)
    {
        var rowStart = row * width;
        for (var column = nearMinX; column <= nearMaxX; column++)
        {
            var tagMask = features[rowStart + column];
            if (tagMask != 0)
            {
                nearFeatures.Add(new PerceivedFeature((short)column, (short)row, tagMask));
            }
        }
    }

    var regionX = x / regionSize;
    var regionY = y / regionSize;
    var mediumRegions = new List<RegionSummary>();
    for (var ry = Math.Max(0, regionY - 3); ry <= Math.Min(regionCountY - 1, regionY + 3); ry++)
    {
        for (var rx = Math.Max(0, regionX - 3); rx <= Math.Min(regionCountX - 1, regionX + 3); rx++)
        {
            var regionId = rx + regionCountX * ry;
            var tagMask = regionTagMasks[regionId];
            if (tagMask != 0)
            {
                mediumRegions.Add(new RegionSummary((ushort)regionId, tagMask, HasStructure: false));
            }
        }
    }

    observations[agent] = new AgentObservation(
        ProtocolVersion: 1,
        Tick: 0,
        Self: new SelfObservation(
            agent,
            position.X,
            position.Y,
            agent % 3 == 0 ? [new InventoryItem("wood", 4 + agent % 8)] : [],
            agent % 2 == 0 ? "stone_axe" : null,
            [new MasteryEntry("foraging", agent % 6)],
            new AgentStatus(agent % 4 == 0, agent % 5 == 0, agent % 2 == 0)),
        Near: nearFeatures.ToArray(),
        Medium: mediumRegions.ToArray(),
        Far: []);
    perceivedFeatureTotal += nearFeatures.Count;
    perceivedRegionTotal += mediumRegions.Count;
}

perceptionTimer.Stop();
ValidatePerceptions(observations, regionSize, regionCountX);
_ = JsonSerializer.SerializeToUtf8Bytes(observations[0], jsonOptions);
_ = JsonSerializer.SerializeToUtf8Bytes(agentPositions, jsonOptions);
var observationSerializationTimer = Stopwatch.StartNew();
long observationBytes = 0;
var maxObservationBytes = 0;
foreach (var observation in observations)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(observation, jsonOptions);
    observationBytes += bytes.Length;
    maxObservationBytes = Math.Max(maxObservationBytes, bytes.Length);
}

var positionsBytes = JsonSerializer.SerializeToUtf8Bytes(agentPositions, jsonOptions).Length;
observationSerializationTimer.Stop();
Console.WriteLine($"B2 perception prototype: {agentCount:N0} agents, compute={perceptionTimer.Elapsed.TotalMilliseconds:F2} ms, JSON encode={observationSerializationTimer.Elapsed.TotalMilliseconds:F2} ms, {perceivedFeatureTotal:N0} near features, {perceivedRegionTotal:N0} medium summaries; JSON avg={observationBytes / agentCount:N0} bytes/observation, max={maxObservationBytes:N0} bytes, all-agent positions={positionsBytes:N0} bytes/tick");

const int measuredTicks = 500;
var scheduledGrowthRuns = new (TimeSpan Elapsed, long Callbacks, long Transitions, int Mature, ulong Checksum)[3];
var randomGrowthRuns = new (TimeSpan Elapsed, long Attempts, long Transitions, int Mature, ulong Checksum)[3];
for (var sample = 0; sample < scheduledGrowthRuns.Length; sample++)
{
    scheduledGrowthRuns[sample] = MeasureScheduledGrowth(width, height, measuredTicks);
    randomGrowthRuns[sample] = MeasureRandomGrowth(width, height, regionSize, regionCountX, measuredTicks, updatesPerRegion: 3);
}

if (scheduledGrowthRuns.Any(run => run.Callbacks != scheduledGrowthRuns[0].Callbacks ||
                                   run.Transitions != scheduledGrowthRuns[0].Transitions ||
                                   run.Mature != scheduledGrowthRuns[0].Mature ||
                                   run.Checksum != scheduledGrowthRuns[0].Checksum) ||
    randomGrowthRuns.Any(run => run.Attempts != randomGrowthRuns[0].Attempts ||
                                run.Transitions != randomGrowthRuns[0].Transitions ||
                                run.Mature != randomGrowthRuns[0].Mature ||
                                run.Checksum != randomGrowthRuns[0].Checksum))
{
    throw new InvalidOperationException("B3 growth results must be deterministic across repeated samples.");
}

var scheduledMedianMs = MedianMilliseconds(scheduledGrowthRuns.Select(run => run.Elapsed.TotalMilliseconds));
var randomMedianMs = MedianMilliseconds(randomGrowthRuns.Select(run => run.Elapsed.TotalMilliseconds));
Console.WriteLine($"B3 growth (all {width * height:N0} crops, {measuredTicks} ticks): scheduled={scheduledMedianMs / measuredTicks:F3} ms/tick, {scheduledGrowthRuns[0].Callbacks:N0} callbacks, {scheduledGrowthRuns[0].Transitions:N0} transitions, {scheduledGrowthRuns[0].Mature:N0} mature; random={randomMedianMs / measuredTicks:F3} ms/tick, {randomGrowthRuns[0].Attempts:N0} attempts, {randomGrowthRuns[0].Transitions:N0} transitions, {randomGrowthRuns[0].Mature:N0} mature");

const int transportTicks = 100;
var tickRequests = Enumerable.Range(0, transportTicks)
    .Select(tick => new TickRequest(1, tick, Enumerable.Range(0, agentCount)
        .Select(agent => new TickCommand($"cmd-{tick:D4}-{agent:D4}", agent, "Move", agent % width, agent / width))
        .ToArray()))
    .ToArray();
var expectedResults = new TickResponse[transportTicks];
var embeddedTimer = Stopwatch.StartNew();
for (var tick = 0; tick < transportTicks; tick++)
{
    expectedResults[tick] = ProcessTick(tickRequests[tick], observations, agentPositions);
}

embeddedTimer.Stop();
var webSocketResult = await MeasureWebSocketTransportAsync(
    tickRequests, expectedResults, observations, agentPositions, jsonOptions);
var commandTotal = (long)transportTicks * agentCount;
var embeddedCommandsPerSecond = commandTotal / embeddedTimer.Elapsed.TotalSeconds;
var webSocketCommandsPerSecond = commandTotal / webSocketResult.Elapsed.TotalSeconds;
Console.WriteLine($"B4 lockstep ({transportTicks} ticks × {agentCount:N0} commands, full synthetic observations/events): embedded={embeddedTimer.Elapsed.TotalMilliseconds:F2} ms ({embeddedCommandsPerSecond:F0} commands/s); loopback WebSocket JSON={webSocketResult.Elapsed.TotalMilliseconds:F2} ms ({webSocketCommandsPerSecond:F0} commands/s), p50={MedianMilliseconds(webSocketResult.TickLatenciesMilliseconds):F2} ms/tick, p95={Percentile(webSocketResult.TickLatenciesMilliseconds, 0.95):F2} ms/tick, {webSocketResult.PayloadBytes:N0} request+response bytes");

var regionSnapshots = new RegionSnapshot[regionCount];
var regionColumns = new ColumnSnapshot[regionCount][];
for (var region = 0; region < regionCount; region++)
{
    var columns = new ColumnSnapshot[regionSize * regionSize];
    var regionX = region % regionCountX;
    var regionY = region / regionCountX;
    for (var localY = 0; localY < regionSize; localY++)
    {
        for (var localX = 0; localX < regionSize; localX++)
        {
            var x = regionX * regionSize + localX;
            var y = regionY * regionSize + localY;
            var index = x + width * y;
            var materialNoise = Mix64((ulong)index ^ 0x6d6174657269616cUL);
            columns[localX + regionSize * localY] = new ColumnSnapshot(
                heights[index],
                (byte)(features[index] == 0 ? 1 : 2),
                (byte)(blocked[index] ? 1 : 0),
                (byte)(materialNoise & 3),
                (byte)((materialNoise >> 8) & 3));
        }
    }

    regionColumns[region] = columns;
    regionSnapshots[region] = new RegionSnapshot(region, 1, columns);
}

var worldSnapshot = new WorldSnapshot(1, 0, regionSnapshots);
var regionDeltas = new RegionDelta[regionCount];
for (var region = 0; region < regionCount; region++)
{
    var changes = new ColumnChange[4];
    for (var change = 0; change < changes.Length; change++)
    {
        var index = (region + change * 61) % (regionSize * regionSize);
        var previous = regionColumns[region][index];
        changes[change] = new ColumnChange(index, previous with { Flags = (byte)(previous.Flags ^ 1) });
    }

    regionDeltas[region] = new RegionDelta(region, 1, 2, changes);
}

var worldDelta = new WorldDelta(1, 1, regionDeltas);
MeasureSnapshotPayload("B5 full snapshot", worldSnapshot, jsonOptions, iterations: 10);
MeasureSnapshotPayload("B5 one-tick delta", worldDelta, jsonOptions, iterations: 10);

static TickResponse ProcessTick(
    TickRequest request, AgentObservation[] observations, AgentPosition[] agentPositions)
{
    if (request.ProtocolVersion != 1)
    {
        throw new InvalidOperationException($"Unsupported protocol version {request.ProtocolVersion}.");
    }

    ulong checksum = 14_695_981_039_346_656_037UL;
    foreach (var command in request.Commands)
    {
        checksum = (checksum ^ (uint)command.AgentId) * 1_099_511_628_211UL;
        checksum = (checksum ^ (uint)command.X) * 1_099_511_628_211UL;
        checksum = (checksum ^ (uint)command.Y) * 1_099_511_628_211UL;
    }

    var tickObservations = observations
        .Select(observation => observation with { Tick = request.Tick })
        .ToArray();
    var events = request.Commands
        .Select(command => new PrismEvent(
            $"evt-{request.Tick:D4}-{command.AgentId:D4}",
            request.Tick,
            "action_completed",
            command.AgentId,
            new ActionCompletedPayload(command.CommandId, "completed")))
        .ToArray();
    return new TickResponse(
        request.ProtocolVersion,
        request.Tick,
        request.Commands.Length,
        tickObservations,
        agentPositions,
        events,
        checksum);
}

static void GenerateRegion(
    int regionId, int width, int height, int regionSize, int regionCountX,
    byte[] heights, bool[] blocked, byte[] features)
{
    var regionX = regionId % regionCountX;
    var regionY = regionId / regionCountX;
    for (var localY = 0; localY < regionSize; localY++)
    {
        var y = regionY * regionSize + localY;
        if ((uint)y >= (uint)height)
        {
            continue;
        }

        for (var localX = 0; localX < regionSize; localX++)
        {
            var x = regionX * regionSize + localX;
            if ((uint)x >= (uint)width)
            {
                continue;
            }

            var index = x + width * y;
            var heightNoise = SampleNoise(x, y, 0x7465727261696eUL);
            heights[index] = (byte)(24 + heightNoise / 16_384);
            var featureNoise = Mix64((ulong)index ^ 0x66656174757265UL);
            blocked[index] = featureNoise % 37 == 0;
            features[index] = featureNoise % 29 == 0 ? (byte)1 : (byte)0;
        }
    }
}

static async Task<WebSocketTransportResult> MeasureWebSocketTransportAsync(
    TickRequest[] requests,
    TickResponse[] expectedResults,
    AgentObservation[] observations,
    AgentPosition[] agentPositions,
    JsonSerializerOptions jsonOptions)
{
    var portProbe = new TcpListener(IPAddress.Loopback, 0);
    portProbe.Start();
    var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
    portProbe.Stop();

    using var listener = new HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    listener.Start();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var serverTask = ServeWebSocketAsync(listener, observations, agentPositions, jsonOptions, cancellation.Token);
    using var client = new ClientWebSocket();
    await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cancellation.Token);

    for (var index = 0; index < Math.Min(10, requests.Length); index++)
    {
        await ExchangeTickAsync(client, requests[index], expectedResults[index], jsonOptions, cancellation.Token);
    }

    var latencies = new double[requests.Length];
    long payloadBytes = 0;
    var timer = Stopwatch.StartNew();
    for (var index = 0; index < requests.Length; index++)
    {
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(requests[index], jsonOptions);
        var tickTimer = Stopwatch.StartNew();
        await client.SendAsync(requestBytes.AsMemory(), WebSocketMessageType.Text, true, cancellation.Token);
        var responseBytes = await ReceiveWebSocketMessageAsync(client, cancellation.Token);
        if (responseBytes is null)
        {
            throw new InvalidOperationException("The WebSocket closed before returning a tick result.");
        }

        var response = JsonSerializer.Deserialize<TickResponse>(responseBytes, jsonOptions)
            ?? throw new InvalidOperationException("The WebSocket returned an empty tick result.");
        tickTimer.Stop();
        ValidateTickResponse(response, expectedResults[index], requests[index].Tick);

        latencies[index] = tickTimer.Elapsed.TotalMilliseconds;
        payloadBytes += requestBytes.Length + responseBytes.Length;
    }

    timer.Stop();
    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "benchmark complete", cancellation.Token);
    await serverTask;
    return new WebSocketTransportResult(timer.Elapsed, payloadBytes, latencies);
}

static async Task ExchangeTickAsync(
    WebSocket socket,
    TickRequest request,
    TickResponse expected,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, jsonOptions);
    await socket.SendAsync(requestBytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    var responseBytes = await ReceiveWebSocketMessageAsync(socket, cancellationToken)
        ?? throw new InvalidOperationException("The WebSocket closed during warm-up.");
    var response = JsonSerializer.Deserialize<TickResponse>(responseBytes, jsonOptions)
        ?? throw new InvalidOperationException("The WebSocket returned an empty warm-up result.");
    ValidateTickResponse(response, expected, request.Tick);
}

static void ValidateTickResponse(TickResponse response, TickResponse expected, int tick)
{
    if (response.ProtocolVersion != expected.ProtocolVersion ||
        response.Tick != expected.Tick ||
        response.ProcessedCommands != expected.ProcessedCommands ||
        response.Checksum != expected.Checksum ||
        !response.Positions.AsSpan().SequenceEqual(expected.Positions) ||
        !response.Events.AsSpan().SequenceEqual(expected.Events) ||
        response.Observations.Length != expected.Observations.Length)
    {
        throw new InvalidOperationException($"WebSocket result mismatch at tick {tick}.");
    }

    foreach (var index in new[] { 0, response.Observations.Length / 2, response.Observations.Length - 1 })
    {
        var actual = response.Observations[index];
        var expectedObservation = expected.Observations[index];
        if (actual.ProtocolVersion != expectedObservation.ProtocolVersion ||
            actual.Tick != expectedObservation.Tick ||
            actual.Self.AgentId != expectedObservation.Self.AgentId ||
            actual.Self.X != expectedObservation.Self.X ||
            actual.Self.Y != expectedObservation.Self.Y ||
            actual.Self.ToolInHand != expectedObservation.Self.ToolInHand ||
            actual.Self.Status != expectedObservation.Self.Status ||
            !actual.Self.Inventory.AsSpan().SequenceEqual(expectedObservation.Self.Inventory) ||
            !actual.Self.Masteries.AsSpan().SequenceEqual(expectedObservation.Self.Masteries) ||
            !actual.Near.AsSpan().SequenceEqual(expectedObservation.Near) ||
            !actual.Medium.AsSpan().SequenceEqual(expectedObservation.Medium) ||
            !actual.Far.AsSpan().SequenceEqual(expectedObservation.Far))
        {
            throw new InvalidOperationException($"WebSocket observation mismatch at tick {tick}, agent {index}.");
        }
    }
}

static async Task ServeWebSocketAsync(
    HttpListener listener,
    AgentObservation[] observations,
    AgentPosition[] agentPositions,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
    if (!context.Request.IsWebSocketRequest)
    {
        throw new InvalidOperationException("The benchmark endpoint only accepts WebSocket requests.");
    }

    var accepted = await context.AcceptWebSocketAsync(subProtocol: null);
    using var socket = accepted.WebSocket;
    while (socket.State == WebSocketState.Open)
    {
        var requestBytes = await ReceiveWebSocketMessageAsync(socket, cancellationToken);
        if (requestBytes is null)
        {
            break;
        }

        var request = JsonSerializer.Deserialize<TickRequest>(requestBytes, jsonOptions)
            ?? throw new InvalidOperationException("The WebSocket received an empty tick request.");
        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(
            ProcessTick(request, observations, agentPositions), jsonOptions);
        await socket.SendAsync(responseBytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    }
}

static async Task<byte[]?> ReceiveWebSocketMessageAsync(WebSocket socket, CancellationToken cancellationToken)
{
    using var message = new MemoryStream();
    var buffer = new byte[64 * 1024];
    while (true)
    {
        var received = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
        if (received.MessageType == WebSocketMessageType.Close)
        {
            if (socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "benchmark complete", cancellationToken);
            }

            return null;
        }

        if (received.MessageType != WebSocketMessageType.Text)
        {
            throw new InvalidOperationException($"Unexpected WebSocket message type {received.MessageType}.");
        }

        message.Write(buffer, 0, received.Count);
        if (received.EndOfMessage)
        {
            return message.ToArray();
        }
    }
}

static double Percentile(double[] samples, double percentile)
{
    if (samples.Length == 0 || percentile is < 0 or > 1)
    {
        throw new ArgumentOutOfRangeException(nameof(percentile));
    }

    var sorted = samples.OrderBy(sample => sample).ToArray();
    var index = Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
    return sorted[index];
}

static void MeasureSnapshotPayload<T>(string name, T payload, JsonSerializerOptions jsonOptions, int iterations)
{
    byte[] serialized = [];
    for (var warmup = 0; warmup < 5; warmup++)
    {
        serialized = JsonSerializer.SerializeToUtf8Bytes(payload, jsonOptions);
        _ = CompressPayload(serialized, useBrotli: false);
        _ = CompressPayload(serialized, useBrotli: true);
    }

    var jsonTimer = Stopwatch.StartNew();
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        serialized = JsonSerializer.SerializeToUtf8Bytes(payload, jsonOptions);
    }

    jsonTimer.Stop();
    long deflateBytes = 0;
    var deflateTimer = Stopwatch.StartNew();
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        deflateBytes += CompressPayload(serialized, useBrotli: false);
    }

    deflateTimer.Stop();
    long brotliBytes = 0;
    var brotliTimer = Stopwatch.StartNew();
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        brotliBytes += CompressPayload(serialized, useBrotli: true);
    }

    brotliTimer.Stop();
    Console.WriteLine($"{name}: JSON={serialized.Length:N0} B, encode={jsonTimer.Elapsed.TotalMilliseconds / iterations:F2} ms; Deflate={deflateBytes / iterations:N0} B, compress={deflateTimer.Elapsed.TotalMilliseconds / iterations:F2} ms; Brotli={brotliBytes / iterations:N0} B, compress={brotliTimer.Elapsed.TotalMilliseconds / iterations:F2} ms");
}

static long CompressPayload(byte[] input, bool useBrotli)
{
    using var output = new MemoryStream();
    using (Stream compressor = useBrotli
               ? new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true)
               : new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
    {
        compressor.Write(input);
    }

    return output.Length;
}

static uint GetRegionTagMask(int regionId, int worldWidth, int size, byte[] cells)
{
    var regionCount = worldWidth / size;
    var regionX = regionId % regionCount;
    var regionY = regionId / regionCount;
    uint mask = 0;
    for (var y = regionY * size; y < (regionY + 1) * size; y++)
    {
        var rowStart = y * worldWidth;
        for (var x = regionX * size; x < (regionX + 1) * size; x++)
        {
            mask |= cells[rowStart + x];
        }
    }

    return mask;
}

static void ValidatePerceptions(AgentObservation[] observations, int regionSize, int regionCountX)
{
    for (var agent = 0; agent < observations.Length; agent++)
    {
        var observation = observations[agent];
        if (observation.Self.AgentId != agent || observation.Far.Length != 0 ||
            observation.Near.Any(feature =>
                Math.Abs(feature.X - observation.Self.X) > 16 ||
                Math.Abs(feature.Y - observation.Self.Y) > 16))
        {
            throw new InvalidOperationException("The generated perception does not match the near/far contract.");
        }

        var regionX = observation.Self.X / regionSize;
        var regionY = observation.Self.Y / regionSize;
        foreach (var summary in observation.Medium)
        {
            var summaryX = summary.RegionId % regionCountX;
            var summaryY = summary.RegionId / regionCountX;
            if (summary.TagMask == 0 || Math.Abs(summaryX - regionX) > 3 || Math.Abs(summaryY - regionY) > 3)
            {
                throw new InvalidOperationException("The generated regional summary is outside the medium perception range.");
            }
        }
    }
}

static (TimeSpan Elapsed, long Callbacks, long Transitions, int Mature, ulong Checksum) MeasureScheduledGrowth(
    int width, int height, int measuredTicks)
{
    const int stageTicks = 40;
    var cropCount = width * height;
    var jitterByCrop = new sbyte[cropCount];
    var eventsByTick = new List<int>?[measuredTicks];
    for (var crop = 0; crop < cropCount; crop++)
    {
        var jitter = (int)(Mix64((ulong)crop ^ 0x67726f777468UL) % 11) - 5;
        jitterByCrop[crop] = (sbyte)jitter;
        var dueTick = stageTicks + jitter;
        (eventsByTick[dueTick] ??= []).Add(crop);
    }

    var stages = new byte[cropCount];
    long callbacks = 0;
    long transitions = 0;
    var timer = Stopwatch.StartNew();
    for (var tick = 0; tick < measuredTicks; tick++)
    {
        if (eventsByTick[tick] is not { } dueCrops)
        {
            continue;
        }

        foreach (var crop in dueCrops)
        {
            callbacks++;
            if (stages[crop] >= 3)
            {
                continue;
            }

            stages[crop]++;
            transitions++;
            if (stages[crop] < 3)
            {
                var nextTick = tick + stageTicks + jitterByCrop[crop];
                (eventsByTick[nextTick] ??= []).Add(crop);
            }
        }
    }

    timer.Stop();
    return (timer.Elapsed, callbacks, transitions, CountMatureCrops(stages), HashCropStages(stages));
}

static (TimeSpan Elapsed, long Attempts, long Transitions, int Mature, ulong Checksum) MeasureRandomGrowth(
    int width, int height, int regionSize, int regionCountX, int measuredTicks, int updatesPerRegion)
{
    var stages = new byte[width * height];
    var regionCount = regionCountX * (height / regionSize);
    long transitions = 0;
    var timer = Stopwatch.StartNew();
    for (var tick = 0; tick < measuredTicks; tick++)
    {
        for (var region = 0; region < regionCount; region++)
        {
            var regionX = region % regionCountX;
            var regionY = region / regionCountX;
            for (var draw = 0; draw < updatesPerRegion; draw++)
            {
                var key = Mix64(0x67726f777468UL ^ ((ulong)(uint)tick << 32) ^
                               ((ulong)(uint)region << 8) ^ (uint)draw);
                var localIndex = (int)(key % (ulong)(regionSize * regionSize));
                var x = regionX * regionSize + localIndex % regionSize;
                var y = regionY * regionSize + localIndex / regionSize;
                var crop = x + width * y;
                if (stages[crop] < 3)
                {
                    stages[crop]++;
                    transitions++;
                }
            }
        }
    }

    timer.Stop();
    return (timer.Elapsed, (long)measuredTicks * regionCount * updatesPerRegion,
        transitions, CountMatureCrops(stages), HashCropStages(stages));
}

static int CountMatureCrops(byte[] stages) => stages.Count(stage => stage == 3);

static ulong HashCropStages(byte[] stages)
{
    var hash = 14_695_981_039_346_656_037UL;
    foreach (var stage in stages)
    {
        hash = (hash ^ stage) * 1_099_511_628_211UL;
    }

    return hash;
}

static double MedianMilliseconds(IEnumerable<double> samples) =>
    samples.OrderBy(sample => sample).ElementAt(samples.Count() / 2);

static int FindOpenCell(int start, bool[] blockedCells)
{
    for (var offset = 0; offset < blockedCells.Length; offset++)
    {
        var index = (start + offset) % blockedCells.Length;
        if (!blockedCells[index])
        {
            return index;
        }
    }

    throw new InvalidOperationException("The generated map has no walkable cells.");
}

static (TimeSpan Elapsed, int Failures, long Expanded) MeasureParallelPaths(
    int agentCount, int width, int height, byte[] heights, bool[] blocked)
{
    var failures = new bool[agentCount];
    var expandedByAgent = new int[agentCount];
    var timer = Stopwatch.StartNew();
    Parallel.For(0, agentCount,
        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
        () => new SurfacePathfinder(width, height, heights, blocked),
        (agent, _, pathfinder) =>
        {
            var key = Mix64((ulong)agent + 0x7061746866696e64UL);
            var start = FindOpenCell((int)(key % (ulong)heights.Length), blocked);
            var target = FindOpenCell((int)(Mix64(key) % (ulong)heights.Length), blocked);
            failures[agent] = !pathfinder.TryFindPath(start, target, out expandedByAgent[agent]);
            return pathfinder;
        },
        _ => { });
    timer.Stop();
    return (timer.Elapsed, failures.Count(failed => failed), expandedByAgent.Sum(expanded => (long)expanded));
}

static (long Pairs, long PermittedDirections, long RejectedDirections) MeasureCardinalSlopeTransitions(
    int width, int height, byte[] heights, bool[] blocked)
{
    long pairs = 0;
    long permittedDirections = 0;
    long rejectedDirections = 0;

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var current = x + width * y;
            if (blocked[current])
            {
                continue;
            }

            if (x + 1 < width)
            {
                CountEdge(current, current + 1);
            }

            if (y + 1 < height)
            {
                CountEdge(current, current + width);
            }
        }
    }

    return (pairs, permittedDirections, rejectedDirections);

    void CountEdge(int first, int second)
    {
        if (blocked[second])
        {
            return;
        }

        pairs++;
        CountDirection(heights[second] - heights[first]);
        CountDirection(heights[first] - heights[second]);
    }

    void CountDirection(int elevationDelta)
    {
        if (elevationDelta is >= -3 and <= 1)
        {
            permittedDirections++;
        }
        else
        {
            rejectedDirections++;
        }
    }
}

static uint SampleNoise(int x, int y, ulong seed)
{
    const int cellSize = 64;
    var cellX = x / cellSize;
    var cellY = y / cellSize;
    var fractionX = (x % cellSize) * (65_536 / cellSize);
    var fractionY = (y % cellSize) * (65_536 / cellSize);
    var topLeft = (int)(Mix64(seed ^ ((ulong)(uint)cellX << 32) ^ (uint)cellY) & 0xFFFF);
    var topRight = (int)(Mix64(seed ^ ((ulong)(uint)(cellX + 1) << 32) ^ (uint)cellY) & 0xFFFF);
    var bottomLeft = (int)(Mix64(seed ^ ((ulong)(uint)cellX << 32) ^ (uint)(cellY + 1)) & 0xFFFF);
    var bottomRight = (int)(Mix64(seed ^ ((ulong)(uint)(cellX + 1) << 32) ^ (uint)(cellY + 1)) & 0xFFFF);
    var top = topLeft + (int)(((long)(topRight - topLeft) * fractionX) >> 16);
    var bottom = bottomLeft + (int)(((long)(bottomRight - bottomLeft) * fractionX) >> 16);
    return (uint)(top + (int)(((long)(bottom - top) * fractionY) >> 16));
}

static ulong Mix64(ulong value)
{
    value ^= value >> 30;
    value *= 0xBF58476D1CE4E5B9UL;
    value ^= value >> 27;
    value *= 0x94D049BB133111EBUL;
    return value ^ (value >> 31);
}

internal sealed record TickCommand(string CommandId, int AgentId, string Kind, int X, int Y);
internal sealed record TickRequest(int ProtocolVersion, int Tick, TickCommand[] Commands);
internal sealed record TickResponse(
    int ProtocolVersion,
    int Tick,
    int ProcessedCommands,
    AgentObservation[] Observations,
    AgentPosition[] Positions,
    PrismEvent[] Events,
    ulong Checksum);
internal sealed record WebSocketTransportResult(TimeSpan Elapsed, long PayloadBytes, double[] TickLatenciesMilliseconds);
internal sealed record ColumnSnapshot(byte SurfaceZ, byte MaterialId, byte Flags, byte BiomeId, byte StrataProfileId);
internal sealed record RegionSnapshot(int RegionId, int Version, ColumnSnapshot[] Columns);
internal sealed record WorldSnapshot(int ProtocolVersion, int Tick, RegionSnapshot[] Regions);
internal sealed record ColumnChange(int Index, ColumnSnapshot Value);
internal sealed record RegionDelta(int RegionId, int BaseVersion, int Version, ColumnChange[] Changes);
internal sealed record WorldDelta(int ProtocolVersion, int Tick, RegionDelta[] Regions);
internal sealed record AgentPosition(int AgentId, short X, short Y);
internal sealed record SelfObservation(
    int AgentId,
    short X,
    short Y,
    InventoryItem[] Inventory,
    string? ToolInHand,
    MasteryEntry[] Masteries,
    AgentStatus Status);
internal sealed record InventoryItem(string ItemId, int Quantity);
internal sealed record MasteryEntry(string SkillId, int Level);
internal sealed record AgentStatus(bool Sheltered, bool OnPath, bool Night);
internal sealed record PrismEvent(
    string EventId, int Tick, string Kind, int? AgentId, ActionCompletedPayload Payload);
internal sealed record ActionCompletedPayload(string CommandId, string Result);
internal sealed record PerceivedFeature(short X, short Y, byte TagMask);
internal sealed record RegionSummary(ushort RegionId, uint TagMask, bool HasStructure);
internal sealed record AgentObservation(
    int ProtocolVersion,
    int Tick,
    SelfObservation Self,
    PerceivedFeature[] Near,
    RegionSummary[] Medium,
    PerceivedFeature[] Far);

internal sealed class SurfacePathfinder(int width, int height, byte[] heights, bool[] blocked)
{
    private readonly int[] _costs = new int[width * height];
    private readonly int[] _searchVersions = new int[width * height];
    private readonly PriorityQueue<int, (int EstimatedTotal, int Heuristic, int Index)> _queue = new();
    private int _searchVersion;

    public bool TryFindPath(int start, int target, out int expanded)
    {
        if ((uint)start >= (uint)_costs.Length || (uint)target >= (uint)_costs.Length ||
            blocked[start] || blocked[target])
        {
            expanded = 0;
            return false;
        }

        if (++_searchVersion == 0)
        {
            Array.Clear(_searchVersions);
            _searchVersion = 1;
        }

        _queue.Clear();
        var initialHeuristic = Heuristic(start, target);
        _searchVersions[start] = _searchVersion;
        _costs[start] = 0;
        _queue.Enqueue(start, (initialHeuristic, initialHeuristic, start));
        expanded = 0;

        while (_queue.TryDequeue(out var current, out var priority))
        {
            var currentCost = priority.EstimatedTotal - priority.Heuristic;
            if (_searchVersions[current] != _searchVersion || _costs[current] != currentCost)
            {
                continue;
            }

            expanded++;
            if (current == target)
            {
                return true;
            }

            var x = current % width;
            var y = current / width;
            Visit(x - 1, y, current, target, 1_000);
            Visit(x, y - 1, current, target, 1_000);
            Visit(x, y + 1, current, target, 1_000);
            Visit(x + 1, y, current, target, 1_000);
            if (IsDiagonalOpen(x - 1, y, x, y - 1))
            {
                Visit(x - 1, y - 1, current, target, 1_414);
            }

            if (IsDiagonalOpen(x + 1, y, x, y - 1))
            {
                Visit(x + 1, y - 1, current, target, 1_414);
            }

            if (IsDiagonalOpen(x - 1, y, x, y + 1))
            {
                Visit(x - 1, y + 1, current, target, 1_414);
            }

            if (IsDiagonalOpen(x + 1, y, x, y + 1))
            {
                Visit(x + 1, y + 1, current, target, 1_414);
            }
        }

        return false;
    }

    private bool IsDiagonalOpen(int firstX, int firstY, int secondX, int secondY) =>
        IsOpen(firstX, firstY) && IsOpen(secondX, secondY);

    private bool IsOpen(int x, int y) =>
        (uint)x < (uint)width && (uint)y < (uint)height && !blocked[x + width * y];

    private void Visit(int x, int y, int current, int target, int movementCost)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return;
        }

        var next = x + width * y;
        if (blocked[next])
        {
            return;
        }

        var elevationDelta = heights[next] - heights[current];
        if (elevationDelta > 1 || elevationDelta < -3)
        {
            return;
        }

        var nextCost = _costs[current] + movementCost + Math.Abs(elevationDelta) * 250;
        if (_searchVersions[next] == _searchVersion && nextCost >= _costs[next])
        {
            return;
        }

        _searchVersions[next] = _searchVersion;
        _costs[next] = nextCost;
        var heuristic = Heuristic(next, target);
        _queue.Enqueue(next, (nextCost + heuristic, heuristic, next));
    }

    private int Heuristic(int from, int to)
    {
        var dx = Math.Abs(from % width - to % width);
        var dy = Math.Abs(from / width - to / width);
        var diagonal = Math.Min(dx, dy);
        var cardinal = Math.Max(dx, dy) - diagonal;
        return diagonal * 1_414 + cardinal * 1_000;
    }
}
