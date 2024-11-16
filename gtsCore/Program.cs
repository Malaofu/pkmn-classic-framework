using GamestatsBase;
using gtsCore.Helpers;
using PkmnFoundations.Data;
using PkmnFoundations.Pokedex;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddGamestatsBaseServices();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IpAddressHelper>();
builder.Services.AddSingleton<Pokedex>(x =>
{
    return new Pokedex(Database.Instance, false);
});
builder.Services.AddTransient<FakeOpponentGenerator>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

//app.UseAuthorization();

app.UseMiddleware<GamestatsBaseMiddleware>();
app.UseMiddleware<BanMiddleware>();

app.MapControllers();

app.Run();
