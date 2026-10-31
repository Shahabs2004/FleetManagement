using FleetManagement.Infrastructure;
using FleetManagement.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// سرویس‌های رابط کاربری Blazor با حالت تعاملی سمت سرور
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// ثبت وابستگی‌های زیرساختی؛ اتصال پایگاه داده پس از تکمیل ویزارد راه‌اندازی فعال می‌شود.
builder.Services.AddFleetInfrastructure(builder.Configuration);

var app = builder.Build();

// پیکربندی خطاها متناسب با محیط اجرا
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// این کلاس برای آزمون‌های یکپارچه‌سازی میزبان وب قابل استفاده است.
public partial class Program;
