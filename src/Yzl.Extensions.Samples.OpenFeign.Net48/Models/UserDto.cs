namespace Yzl.Extensions.Samples.OpenFeign.Net48.Models
{
    public class UserDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? City { get; set; }

        public UserDto() { }
        public UserDto(long id, string name, int age, string? city = null)
        {
            Id = id;
            Name = name;
            Age = age;
            City = city;
        }
    }
}
