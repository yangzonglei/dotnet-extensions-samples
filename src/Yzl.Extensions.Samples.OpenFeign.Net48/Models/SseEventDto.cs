namespace Yzl.Extensions.Samples.OpenFeign.Net48.Models
{
    /// <summary>
    /// SSE 事件 DTO（Net48 不支持 SSE 流式消费，此处仅作为模型定义保留）
    /// </summary>
    public class SseEventDto
    {
        public int Index { get; set; }
        public string? Message { get; set; }
        public bool CompleteSucc { get; set; }
    }
}
