using System;
using System.Reflection;
using System.Text.Json;
using Yzl.Extensions.Http.OpenFeign.Execution;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Resolvers
{
    /// <summary>
    /// 自定义响应解析器 — 解析 {"status":"success","result":...,"message":"..."} 格式。
    /// 通过 [FeignResponse] 特性显式绑定到目标方法，而非 CanResolve 隐式匹配。
    /// </summary>
    public sealed class StatusResponseResolver : IFeignResponseResolver
    {
        public int Order => 1;
        public bool IsGlobal => false;

        /// <summary>
        /// 此解析器通过 [FeignResponse] 特性显式声明绑定，不使用 CanResolve 隐式匹配。
        /// </summary>
        public bool CanResolve(MethodInfo method) => false;

        public object? Resolve(
            JsonElement root,
            Type resultType,
            FeignRequestContext context,
            IFeignSerializer serializer)
        {
            if (!root.TryGetProperty("status", out var statusElement) ||
                !string.Equals(statusElement.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (resultType == typeof(string))
            {
                return resultElement.ValueKind == JsonValueKind.String
                    ? resultElement.GetString()
                    : resultElement.GetRawText();
            }

            if (resultType == typeof(object))
            {
                return resultElement.Clone();
            }

            return serializer.Deserialize(resultElement, resultType);
        }
    }
}
