using PkmnFoundations.GlobalTerminalService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GlobalTerminalService";
});

builder.Services.AddHostedService<Service1>();

var host = builder.Build();
host.Run();
