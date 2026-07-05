using System.Text.Json;
using Yzl.Extensions.Http.OpenFeign.Execution;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.FeignImpl;

/// <summary>
/// {"status": 200, "result": "xxx" , "message": "xxx"}
/// </summary>
public sealed class ResultFeignResponseResolver : IFeignResponseResolver
{
    public int Order => 1;

    public bool IsGlobal => false;

    public object? Resolve(
        JsonElement root,
        Type resultType,
        FeignRequestContext context,
        IFeignSerializer serializer)
    {
        if (!root.TryGetProperty("status", out var statusElement) ||
            !statusElement.TryGetInt32(out var status) ||
            status != 200)
        {
            return null;
        }

        if (!root.TryGetProperty("result", out var resultElement))
            return null;

        if (resultElement.ValueKind == JsonValueKind.Null)
            return null;

        if (resultType == typeof(string))
        {
            return resultElement.ValueKind == JsonValueKind.String
                ? resultElement.GetString()
                : resultElement.GetRawText();
        }

        if (resultType == typeof(object))
            return resultElement.Clone();

        return serializer.Deserialize(resultElement, resultType);
    }
}
