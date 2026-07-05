namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public abstract class BaseEntity
{
    public override string ToString()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this);
    }
}
