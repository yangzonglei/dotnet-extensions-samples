using Yzl.Extensions.Samples.TestDashboard;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║                Samples API                            ║
                  ║                                                      ║
                  ║     访问: http://localhost:16600                      ║
                  ║                                                      ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:16600");
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapGet("/", () => "Yzl.Extensions.Http.OpenFeign Samples API");
app.MapControllers();
app.MapTestDashboard();

app.Run();
