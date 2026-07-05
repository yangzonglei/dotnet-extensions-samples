using System.Collections.Generic;
using Yzl.Extensions.Http.OpenFeign.Headers;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.HeaderProviders
{
    /// <summary>
    /// 全局请求头提供器 — 为所有 Feign 请求添加 X-Demo-Global 和 Authorization 头。
    /// Order=-100 高优先级，会被自动注册。
    /// </summary>
    public sealed class DemoHeaderProvider : IFeignRequestHeaderProvider
    {
        public int Order => -100;

        public void Apply(IDictionary<string, string> headers)
        {
            if (!headers.ContainsKey("X-Demo-Global"))
                headers.Add("X-Demo-Global", "from-header-provider");
            if (!headers.ContainsKey("Authorization"))
                headers.Add("Authorization", "Bearer demo-token");
        }
    }
}
