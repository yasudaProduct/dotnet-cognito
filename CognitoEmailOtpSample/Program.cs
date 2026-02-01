using Amazon.CognitoIdentityProvider;
using CognitoEmailOtpSample.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cookie 認証の設定
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/SignIn";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

// AWS Cognito クライアントの登録
var region = builder.Configuration["AWS:Region"] ?? "ap-northeast-1";
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
{
    var config = new AmazonCognitoIdentityProviderConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
    };
    return new AmazonCognitoIdentityProviderClient(config);
});

// サービスの登録
builder.Services.AddScoped<ICognitoEmailOtpService, CognitoEmailOtpService>();
builder.Services.AddScoped<ILocalAuthService, LocalAuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=SignIn}/{id?}");

app.Run();
