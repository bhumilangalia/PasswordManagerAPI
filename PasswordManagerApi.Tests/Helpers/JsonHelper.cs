using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasswordManagerApi.Tests.Helpers;

/// <summary>
/// Helper for JSON deserialization in tests that matches the API's JSON configuration.
/// The API uses JsonStringEnumConverter to serialize enums as strings (e.g., "Weak" instead of 1).
/// Test HTTP clients need to use the same converter when deserializing responses.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// JSON options matching the API configuration (Program.cs line 28).
    /// Includes JsonStringEnumConverter for enum string serialization.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Deserializes JSON from HTTP content using API-compatible JSON options.
    /// Use this instead of ReadFromJsonAsync() to avoid enum deserialization errors.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="content">The HTTP response content</param>
    /// <returns>Deserialized object or null</returns>
    public static async Task<T?> DeserializeAsync<T>(HttpContent content)
    {
        var stream = await content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, Options);
    }
}
