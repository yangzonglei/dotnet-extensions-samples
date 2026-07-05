using System.Text.Json;

namespace Samples.Models;

public abstract class BaseEntity
{
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
