namespace Yzl.Extensions.Samples.OpenFeign.Net48.Models
{
    public class UpdateUserRequest
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? City { get; set; }

        public UpdateUserRequest() { }
        public UpdateUserRequest(string name, int age, string? city = null)
        {
            Name = name;
            Age = age;
            City = city;
        }
    }
}
