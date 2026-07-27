using PewPew.SharedKernel.Configuration;

_ = StartupConfiguration.LoadFromEnvironment();

var builder = WebApplication.CreateBuilder(args);
var application = builder.Build();

application.MapGet("/health", () => Results.Ok(new { status = "ok" }));

application.Run();
