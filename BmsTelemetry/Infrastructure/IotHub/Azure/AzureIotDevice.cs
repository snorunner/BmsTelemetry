using System.Text;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using System.Text.Json.Nodes;
using System.Text.Json;

public sealed class AzureIotDevice : IIotDevice, IAsyncDisposable
{
    private readonly ILogger<AzureIotDevice> _logger;
    private readonly DpsService _dpsService;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private const int MaxMessageBytes = 252_000;

    private DeviceClient? _deviceClient;

    public ConnectionStatus Connected { get; private set; }

    public AzureIotDevice(DpsService dpsService, ILogger<AzureIotDevice> logger)
    {
        Connected = ConnectionStatus.Unknown;
        _dpsService = dpsService;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (Connected == ConnectionStatus.Connected)
        {
            return;
        }

        await _connectionLock.WaitAsync(ct);

        try
        {
            if (Connected == ConnectionStatus.Connected)
            {
                return;
            }

            Connected = ConnectionStatus.Connecting;
            _deviceClient ??= await _dpsService.ProvisionDeviceAsync();

            await _deviceClient.OpenAsync(ct);
            _logger.LogInformation("Azure IoT device has been connected successfully.");
            Connected = ConnectionStatus.Connected;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static IEnumerable<JsonObject> NormalizeForIoTHub(JsonNode diff)
    {
        if (diff is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is null)
                    continue;

                var obj = item as JsonObject
                          ?? new JsonObject { ["value"] = item };

                // FILTER HERE
                if (!obj.ContainsKey("data"))
                    continue;

                yield return obj;
            }
        }
        else if (diff is JsonObject obj)
        {
            if (obj.ContainsKey("data"))
                yield return obj;
        }
        else
        {
            // Non-object payloads cannot have "data", so skip them
            yield break;
        }
    }


    private static List<Message> ChunkMessages(IEnumerable<JsonObject> jsonPayloads)
    {
        var list = jsonPayloads.ToList();
        var messages = new List<Message>();

        int index = 0;

        while (index < list.Count)
        {
            int left = 1;
            int right = list.Count - index;
            int bestFit = 1;

            // Binary search for largest chunk that fits
            while (left <= right)
            {
                int mid = (left + right) / 2;

                var chunk = new JsonArray();
                for (int i = 0; i < mid; i++)
                    chunk.Add(list[index + i].DeepClone());

                var json = JsonSerializer.Serialize(chunk);
                int size = Encoding.UTF8.GetByteCount(json);

                if (size <= MaxMessageBytes)
                {
                    bestFit = mid;
                    left = mid + 1;   // try larger
                }
                else
                {
                    right = mid - 1;  // try smaller
                }
            }

            // Build the final chunk using bestFit
            var finalChunk = new JsonArray();
            for (int i = 0; i < bestFit; i++)
                finalChunk.Add(list[index + i].DeepClone());

            var finalJson = JsonSerializer.Serialize(finalChunk);
            var message = new Message(Encoding.UTF8.GetBytes(finalJson));

            messages.Add(message);

            index += bestFit; // move forward
        }

        return messages;
    }

    public async Task SendMessageAsync(JsonNode payload, CancellationToken ct = default)
    {
        await ConnectAsync(ct);

        var enumerablePayload = NormalizeForIoTHub(payload);
        var messagesToSend = ChunkMessages(enumerablePayload);

        foreach (var message in messagesToSend)
        {
            try
            {
                await _deviceClient!.SendEventAsync(message, ct);
                _logger.LogInformation("Message sent to Azure IotHub successfully");
                await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            }
            catch (IotHubException ex)
            {
                _logger.LogError(ex, "Failed to send {payload} to Azure IotHub.", payload);
                Connected = ConnectionStatus.Disconnecting;
                throw;
            }
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_deviceClient is not null)
            {
                await _deviceClient.CloseAsync(ct);
                Connected = ConnectionStatus.Disconnecting;
            }
        }
        finally
        {
            _logger.LogInformation("Azure Iot Device has been disconnected successfully.");
            _connectionLock.Release();
            Connected = ConnectionStatus.Disconnected;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_deviceClient is not null)
        {
            _logger.LogDebug("Disposing of Azure IoT device.");
            await _deviceClient.DisposeAsync();
        }
    }
}
