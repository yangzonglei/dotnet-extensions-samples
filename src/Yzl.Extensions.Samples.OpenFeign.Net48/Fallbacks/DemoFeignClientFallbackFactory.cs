using System;
using System.Threading.Tasks;
using Yzl.Extensions.Http.OpenFeign.Abstractions;
using Yzl.Extensions.Samples.OpenFeign.Net48.Clients;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks
{
    /// <summary>
    /// 【OpenFeign 降级工厂 —— 对齐 Java Spring Cloud OpenFeign 的 FallbackFactory】
    ///
    /// 与直接写 fallback: typeof(...) 的区别：工厂能在降级前拿到
    /// 「导致降级的异常」，从而产出「带原因」的降级实现。
    ///
    /// 框架侧的调用时序：
    ///   1. 远程调用失败（连接被拒 / 超时 / 5xx …）
    ///   2. 拦截器的 catch 分支调用 Create，把异常作为 cause 传进来
    ///   3. 用工厂返回的实例调用同名方法，调用方拿到降级值
    /// </summary>
    public class DemoFeignClientFallbackFactory : IFeignFallbackFactory<IDemoFeignClientFactory>
    {
        /// <summary>
        /// 最近一次降级的原因（仅用于演示 / 诊断）。
        ///
        /// 降级实现是通过工厂 new 出来的，调用方拿不到那个实例，
        /// 因此用静态字段把「最近一次原因」留出来给控制台输出展示。
        /// 生产代码不该这样用静态可变状态，这里只是为了让「fallbackFactory 能拿到原因」肉眼可见。
        /// </summary>
        public static string LastCause { get; private set; }

        /// <summary>
        /// 每次降级都新建一个降级实现，并把本次的 cause 带进去。
        /// </summary>
        public IDemoFeignClientFactory Create(Exception cause)
        {
            LastCause = Describe(cause);
            return new DemoFeignClientFactoryFallback(cause);
        }

        /// <summary>
        /// 把异常整理成一行可读的原因，优先展示最内层（超时/连接失败的实际异常通常是 InnerException）。
        /// </summary>
        public static string Describe(Exception cause)
        {
            var root = cause;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return ReferenceEquals(root, cause)
                ? cause.GetType().Name + ": " + cause.Message
                : cause.GetType().Name + " → " + root.GetType().Name + ": " + root.Message;
        }
    }

    /// <summary>
    /// 降级实现 —— 与 DemoFeignClientFallback 返回同类哨兵值，但多了一层「失败原因」，便于排查。
    /// </summary>
    /// <remarks>
    /// 本类刻意不是无参构造：fallback 那条路径由 DI 解析，
    /// 而 fallbackFactory 这条路径由工厂 new 出来，因此可以接收原因。
    /// </remarks>
    public class DemoFeignClientFactoryFallback : IDemoFeignClientFactory
    {
        /// <summary>本次降级的直接原因（InnerException 常是真正的超时/连接失败）。</summary>
        public Exception Cause { get; }

        public DemoFeignClientFactoryFallback(Exception cause)
        {
            Cause = cause;
        }

        /// <summary>同步降级值。</summary>
        public string TimeoutWithFallback()
        {
            Console.WriteLine($"  [Feign 降级·工厂] 同步超时，原因：{DemoFeignClientFallbackFactory.Describe(Cause)}");
            return "factory fallback";
        }

        /// <summary>
        /// 异步降级值 —— 返回类型是 Task&lt;string&gt;，
        /// 用来证明「async 返回值同样会走工厂」。
        /// </summary>
        public Task<string> TimeoutWithFallbackAsync()
        {
            Console.WriteLine($"  [Feign 降级·工厂] 异步超时，原因：{DemoFeignClientFallbackFactory.Describe(Cause)}");
            return Task.FromResult("factory fallback (async)");
        }
    }
}
