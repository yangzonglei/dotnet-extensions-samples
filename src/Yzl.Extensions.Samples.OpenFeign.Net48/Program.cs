using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Clients;
using Yzl.Extensions.Samples.OpenFeign.Net48.Models;

namespace Yzl.Extensions.Samples.OpenFeign.Net48
{
    /// <summary>
    /// OpenFeign.Net48 示例客户端控制台应用
    ///
    /// 前置条件：启动 OpenFeign.Samples.Api 项目（http://localhost:16600）。
    ///
    /// 本示例覆盖：
    /// - GET / POST / PUT / DELETE / PATCH / HEAD / OPTIONS / TRACE 请求
    /// - PathVariable / RequestParam / RequestBody / RequestHeader / QueryMap 参数绑定
    /// - 同步/异步返回类型
    /// - RawFormat 切换（true=原始响应包装 / false=自动解包）
    /// - 自定义 ResponseResolver（StatusResponseResolver 解析 status/result/message 格式）
    /// - 文件下载（Stream 同步/异步 和 byte[]）
    /// - Fallback 容错
    /// - 超时熔断
    /// - 五种请求体类型（DTO / string / byte[] / Stream / StringContent）
    /// - 全局 HeaderProvider 自动注入
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║   Yzl.Extensions.Http.OpenFeign .NET 4.8 测试       ║
                  ║                                                      ║
                  ║     .NET Framework 4.8 上演示 OpenFeign             ║
                  ║     CRUD / HTTP 方法 / 请求体 / 文件下载            ║
                  ║                                                      ║
                  ║     前置依赖: Samples.Api (端口 16600)               ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

            // ============================================================
            // 1. 注册 OpenFeign 客户端（实际项目中放在 Global.asax Application_Start）
            // ============================================================
            FeignConfig.Register();
            Console.WriteLine("FeignConfig 已初始化");
            Console.WriteLine();

            // ============================================================
            // 2. 测试开始
            // ============================================================
            Console.WriteLine("测试开始 — 请确保 OpenFeign.Samples.Api 已在 http://localhost:16600 运行");
            Console.WriteLine("按 Enter 继续...");
            Console.ReadLine();

            try
            {
                // ============================================================
                // 3. 通过 FeignConfig 获取 Feign 客户端
                // ============================================================
                var demoClient = FeignConfig.GetFeignClient<IDemoFeignClient>();
                var bodyClient = FeignConfig.GetFeignClient<IRequestBodyDemoFeignClient>();
                var downloadClient = FeignConfig.GetFeignClient<IDownloadClient>();

                Console.WriteLine();
                Console.WriteLine("=== 客户端代理信息 ===");
                Console.WriteLine($"  IDemoFeignClient 类型: {demoClient.GetType().FullName}");
                Console.WriteLine($"  IRequestBodyDemoFeignClient 类型: {bodyClient.GetType().FullName}");
                Console.WriteLine($"  IDownloadClient 类型: {downloadClient.GetType().FullName}");
                Console.WriteLine($"  是 Castle 代理: {typeof(Castle.DynamicProxy.IProxyTargetAccessor).IsAssignableFrom(demoClient.GetType())}");
                Console.WriteLine();

                // ============================================================
                // 3.1 基础 CRUD 演示
                // ============================================================
                WriteSection("基础 CRUD 操作");

                var user = await demoClient.GetById(1);
                Console.WriteLine($"  GetById(1): Id={user?.Id}, Name={user?.Name}, Age={user?.Age}");

                var userSync = demoClient.GetByIdSync(2);
                Console.WriteLine($"  GetByIdSync(2): Id={userSync?.Id}, Name={userSync?.Name}, Age={userSync?.Age}");

                var queryResult = await demoClient.Query(3, "Tom");
                Console.WriteLine($"  Query(3, Tom): {queryResult}");

                var mapResult = await demoClient.QueryMap(new Dictionary<string, string>
                {
                    ["age"] = "18",
                    ["city"] = "Shanghai"
                });
                Console.WriteLine($"  QueryMap: {mapResult}");

                var createdUser = await demoClient.Create(new CreateUserRequest("Jerry", 20, "Hangzhou"));
                Console.WriteLine($"  Create: Id={createdUser?.Id}, Name={createdUser?.Name}, Age={createdUser?.Age}");

                var updatedUser = await demoClient.Update(4, new UpdateUserRequest("Bob", 30, "Shenzhen"));
                Console.WriteLine($"  Update: Id={updatedUser?.Id}, Name={updatedUser?.Name}, Age={updatedUser?.Age}");

                var deleted = await demoClient.Delete(4);
                Console.WriteLine($"  Delete: {deleted}");

                // ============================================================
                // 3.2 HTTP 方法演示
                // ============================================================
                WriteSection("HTTP 方法");

                await demoClient.Head();
                Console.WriteLine("  HEAD /api/methods/head: 完成");

                var patchResult = await demoClient.Patch(5, new Dictionary<string, object?>
                {
                    ["name"] = "Patch User",
                    ["city"] = "Beijing"
                });
                Console.WriteLine($"  PATCH: Id={patchResult?.Id}, Name={patchResult?.Name}");

                var optionsResult = await demoClient.Options();
                Console.WriteLine($"  OPTIONS: {optionsResult}");

                var traceResult = await demoClient.Trace();
                Console.WriteLine($"  TRACE: {traceResult}");

                // ============================================================
                // 3.3 高级功能
                // ============================================================
                WriteSection("高级功能");

                var headersResult = await demoClient.Headers("token-from-parameter");
                Console.WriteLine($"  RequestHeader: {headersResult}");

                var wrappedData = await demoClient.GetWrappedData(6);
                Console.WriteLine($"  RawFormat=false: Id={wrappedData?.Id}, Name={wrappedData?.Name}");

                var wrappedRaw = await demoClient.GetWrappedRaw(7);
                Console.WriteLine($"  RawFormat=true: Code={wrappedRaw?.Code}, Data.Id={wrappedRaw?.Data?.Id}, Msg={wrappedRaw?.Msg}");

                var statusResult = await demoClient.GetStatusResult(8);
                Console.WriteLine($"  Custom Resolver: Id={statusResult?.Id}, Name={statusResult?.Name}");

                Console.WriteLine("  请求 /api/timeout (timeout=1000ms, 服务端延迟3000ms)...");
                var timeoutResult = demoClient.TimeoutWithFallback();
                Console.WriteLine($"  Timeout Fallback: \"{timeoutResult}\"");

                // ============================================================
                // 3.4 请求体类型演示
                // ============================================================
                WriteSection("请求体类型");

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("stream body demo")))
                {
                    var objectBody = await bodyClient.SendObject(new CreateUserRequest("Body User", 28, "Guangzhou"));
                    Console.WriteLine($"  Object body: Id={objectBody?.Id}, Name={objectBody?.Name}");

                    var stringBody = await bodyClient.SendString("plain string body");
                    Console.WriteLine($"  String body: {stringBody}");

                    var bytesBody = await bodyClient.SendBytes(Encoding.UTF8.GetBytes("byte array body"));
                    Console.WriteLine($"  Byte[] body: {bytesBody}");

                    var streamBody = await bodyClient.SendStream(stream);
                    Console.WriteLine($"  Stream body: {streamBody}");

                    var httpContentBody = await bodyClient.SendHttpContent(
                        RequestBodyDemoFeignClientHelper.CreateJsonContent("{\"name\":\"http-content\"}"));
                    Console.WriteLine($"  HttpContent body: {httpContentBody}");
                }

                // ============================================================
                // 3.5 文件下载
                // ============================================================
                WriteSection("文件下载");

                using (var stream = await downloadClient.DownloadAsync())
                using (var reader = new StreamReader(stream))
                {
                    var content = await reader.ReadToEndAsync();
                    Console.WriteLine($"  DownloadAsync: {content.Length} chars — \"{content}\"");
                }

                using (var stream = downloadClient.Download())
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    Console.WriteLine($"  Download (sync): {content.Length} chars — \"{content}\"");
                }

                var bytes = await downloadClient.DownloadBytesAsync();
                if (bytes != null)
                {
                    Console.WriteLine($"  DownloadBytesAsync: {bytes.Length} bytes — \"{Encoding.UTF8.GetString(bytes)}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[ERROR] {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("============================================");
            Console.WriteLine("所有测试完成。");
            Console.WriteLine("按 Enter 退出...");
            Console.ReadLine();
        }

        static void WriteSection(string name)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {name} ---");
        }
    }
}
