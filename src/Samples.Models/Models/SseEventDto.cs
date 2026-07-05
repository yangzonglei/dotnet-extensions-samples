namespace Samples.Models;

public sealed record SseEventDto(int Index, string Message, bool CompleteSucc = false);
