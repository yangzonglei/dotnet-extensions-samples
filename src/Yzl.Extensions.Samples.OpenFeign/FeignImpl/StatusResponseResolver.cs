using System.Text.Json;
using Yzl.Extensions.Http.OpenFeign.Execution;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.FeignImpl;

public sealed class StatusResponseResolver : IFeignResponseResolver
{
    public int Order => 1;

    public bool IsGlobal => false;

    public object? Resolve(JsonElement root, Type resultType, FeignRequestContext context, IFeignSerializer serializer)
    {
        if (!root.TryGetProperty("status", out var statusElement) ||
            !string.Equals(statusElement.GetString(), "success", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!root.TryGetProperty("result", out var resultElement) || resultElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (resultType == typeof(string))
        {
            return resultElement.ValueKind == JsonValueKind.String ? resultElement.GetString() : resultElement.GetRawText();
        }

        if (resultType == typeof(object))
        {
            return resultElement.Clone();
        }

        return serializer.Deserialize(resultElement, resultType);
    }
}
