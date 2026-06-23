namespace InventoryBuddy.Shared.DTOs;

/// <summary>
/// What the ESP32 camera sends when uploading an image.
/// </summary>
public class ImageUploadRequest
{
    public string CameraMac { get; set; } = string.Empty;
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string ImageFormat { get; set; } = "jpeg";           // jpeg, png
    public DateTime CapturedAt { get; set; }
}
