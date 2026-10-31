using FleetManagement.Infrastructure;
using FleetManagement.Infrastructure.Bootstrap;
using FleetManagement.Web.Components;
using FleetManagement.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// سرویس‌های رابط کاربری Blazor با حالت تعاملی سمت سرور
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// ثبت سرویس‌های زیرساختی؛ اتصال SQL پس از تکمیل ویزارد از فایل محافظت‌شده خوانده می‌شود.
builder.Services.AddFleetInfrastructure(builder.Configuration);
builder.Services.AddFleetAuthentication();

var app = builder.Build();

// آماده‌سازی کد اولیه فقط در اولین اجرا؛ کد خام در فایل محلی محدودشده قرار می‌گیرد.
using (var scope = app.Services.CreateScope())
{
    var bootstrapSetup = scope.ServiceProvider.GetRequiredService<BootstrapSetupService>();
    await bootstrapSetup.EnsureSetupCodeAsync();
}

// پیکربندی خطاها متناسب با محیط اجرا
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// مسیرهای ورود و خروج با کنترل توکن ضد جعل ثبت می‌شوند.
app.MapFleetAuthentication();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// این کلاس برای آزمون‌های یکپارچه‌سازی میزبان وب قابل استفاده است.
public partial class Program;
