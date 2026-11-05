using System.Text.Json;
using System.Text.Json.Serialization;

namespace KRT.Onboarding.Infrastructure.Serialization;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
