namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class AnswerKeyQO
{
    public int quesId { get; set; }
    public int userId { get; set; }
    /// <summary>
    /// 应用类型，1-主站base，2-Tencent，3-pingan，4-dj，5-null
    /// </summary>
    public string appType { get; set; } = "1";
}
