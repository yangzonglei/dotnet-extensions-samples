namespace Samples.Models;

public class RandomChineseSseDto
{
    public int batchNumber { get; set; }
    public int totalBatches { get; set; }
    public int receivedChars { get; set; }
    public string content { get; set; }
    public double progress { get; set; }

    // 完成时
    public bool completeSucc { get; set; }
    public int totalChars { get; set; }
    public string fullText { get; set; }
}
