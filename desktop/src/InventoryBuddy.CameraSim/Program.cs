using System.Net.Http.Headers;

// ====================================================================
//  InventoryBuddy.CameraSim — a console app that pretends to be
//  an ESP32 camera node.  Useful for testing the Hub without hardware.
//
//  Usage:
//    dotnet run -- --mac AA:BB:CC:DD:EE:FF --hub-url http://localhost:5000 --image-dir ./test-images
// ====================================================================

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    // ---------- parse arguments ----------
    string? mac = null;
    string hubUrl = "http://inventorybuddy.local:8000";
    string imageDir = "./test-images";
    int delayMs = 5_000;          // between image sends

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--mac" when i + 1 < args.Length:
                mac = args[++i];
                break;
            case "--hub-url" when i + 1 < args.Length:
                hubUrl = args[++i].TrimEnd('/');
                break;
            case "--image-dir" when i + 1 < args.Length:
                imageDir = args[++i];
                break;
            case "--delay" when i + 1 < args.Length:
                delayMs = int.Parse(args[++i]);
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(mac))
    {
        Console.Error.WriteLine("ERROR: --mac <MAC address> is required.");
        return 1;
    }

    if (!Directory.Exists(imageDir))
    {
        Console.Error.WriteLine($"ERROR: image directory '{imageDir}' not found.");
        return 1;
    }

    // Find JPEG images
    var images = Directory.EnumerateFiles(imageDir, "*.*")
        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                 || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                 || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f)
        .ToList();

    if (images.Count == 0)
    {
        Console.Error.WriteLine($"ERROR: no JPEG/PNG images found in '{imageDir}'.");
        return 1;
    }

    Console.WriteLine($"CameraSim starting — MAC: {mac}, Hub: {hubUrl}, {images.Count} images, delay: {delayMs}ms");
    Console.WriteLine("Press Ctrl+C to stop.");

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var cts = new CancellationTokenSource();

    // Handle Ctrl+C
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // Start heartbeat loop on a background thread
    var heartbeatTask = HeartbeatLoopAsync(http, hubUrl, mac, cts.Token);

    // ---------- main send loop ----------
    int imageIndex = 0;
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var imagePath = images[imageIndex % images.Count];
            imageIndex++;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sending {Path.GetFileName(imagePath)}...");

            try
            {
                await using var fileStream = File.OpenRead(imagePath);
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue(imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? "image/png"
                        : "image/jpeg");

                content.Add(fileContent, "image", Path.GetFileName(imagePath));

                var response = await http.PostAsync($"{hubUrl}/api/camera/{mac}/image", content, cts.Token);
                var body = await response.Content.ReadAsStringAsync(cts.Token);

                Console.WriteLine($"  -> {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"  -> {body}");
                else
                    Console.WriteLine($"  -> {body[..Math.Min(body.Length, 120)]}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ERROR: {ex.Message}");
            }

            await Task.Delay(delayMs, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        // expected on shutdown
    }

    Console.WriteLine("Shutting down...");
    cts.Cancel();

    try { await heartbeatTask; } catch { /* ignore */ }

    return 0;
}

/// <summary>
/// Sends a heartbeat POST every 30 seconds in the background.
/// </summary>
static async Task HeartbeatLoopAsync(HttpClient http, string hubUrl, string mac, CancellationToken ct)
{
    // Simulated battery voltage that slowly drifts down
    float battery = 4.1f;

    while (!ct.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(30_000, ct);

            battery -= 0.01f;
            if (battery < 3.0f) battery = 4.2f; // "recharge"

            var payload = $$"""{"mac_address":"{{mac}}","battery_voltage":{{battery:F2}}}""";
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var resp = await http.PostAsync($"{hubUrl}/api/camera/heartbeat", content, ct);

            if (resp.IsSuccessStatusCode)
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Heartbeat sent — battery {battery:F2}V");
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Heartbeat ERROR: {ex.Message}");
        }
    }
}
