namespace Samples.Models;

public sealed record UpdateUserRequest(string Name, int Age, string? City = null);
