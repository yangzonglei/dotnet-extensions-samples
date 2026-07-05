using System.Text.Json;
using Yzl.Extensions.Http.OpenFeign.Execution;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.FeignImpl;

public sealed class SampleResponseResolver : IFeignResponseResolver
{
    public int Order => 1;

    public bool IsGlobal => true;

    public object? Resolve(JsonElement root, Type resultType, FeignAotRequestContext context, IFeignSerializer serializer)
    {
        if (TryResolveCodeResult(root, resultType, serializer, out var codeResult))
        {
            return codeResult;
        }

        if (TryResolveStatusResult(root, resultType, serializer, out var statusResult))
        {
            return statusResult;
        }

        return serializer.Deserialize(root, resultType);
    }

    private static bool TryResolveCodeResult(JsonElement root, Type resultType, IFeignSerializer serializer, out object? result)
    {
        result = null;

        if (!root.TryGetProperty("code", out var codeElement) ||
            !codeElement.TryGetInt32(out var code) ||
            code != 0 ||
            !root.TryGetProperty("data", out var dataElement))
        {
            return false;
        }

        result = ResolveElement(dataElement, resultType, serializer);
        return true;
    }

    private static bool TryResolveStatusResult(JsonElement root, Type resultType, IFeignSerializer serializer, out object? result)
    {
        result = null;

        if (!root.TryGetProperty("status", out var statusElement) ||
            !string.Equals(statusElement.GetString(), "success", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("result", out var resultElement))
        {
            return false;
        }

        result = ResolveElement(resultElement, resultType, serializer);
        return true;
    }

    private static object? ResolveElement(JsonElement element, Type resultType, IFeignSerializer serializer)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (resultType == typeof(string))
        {
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }

        if (resultType == typeof(object) || resultType == typeof(JsonElement))
        {
            return element.Clone();
        }

        return serializer.Deserialize(element, resultType);
    }
}
