using System.Text.Json.Serialization;
using RoomReservation.Web.Components;
using RoomReservation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// กำหนดพอร์ตมาตรฐาน 5180 เพื่อให้ทั้ง Web Browser และแอป Windows Forms เชื่อมต่อได้ทันที
builder.WebHost.UseUrls("http://localhost:5180");

builder.Services.AddSingleton<ReservationEngineService>();
builder.Services.AddHostedService<AutoReleaseSchedulerService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
