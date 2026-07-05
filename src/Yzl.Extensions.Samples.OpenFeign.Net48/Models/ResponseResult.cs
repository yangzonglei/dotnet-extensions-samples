namespace Yzl.Extensions.Samples.OpenFeign.Net48.Models
{
    /// <summary>
    /// 统一响应结果（code/data/msg 格式）
    /// </summary>
    public class ResponseResult<T>
    {
        public int Code { get; set; }
        public T? Data { get; set; }
        public string? Msg { get; set; }
    }
}
