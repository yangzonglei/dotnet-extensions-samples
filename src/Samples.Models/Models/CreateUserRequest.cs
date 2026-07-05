namespace Samples.Models;

public sealed record CreateUserRequest(string Name, int Age, string? City = null);
