using Google.Cloud.SecretManager.V1;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.CodeAnalysis;
using SWD63AMovieUploader.DataAccess;
using System.Configuration;

IConfiguration configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();

var builder = WebApplication.CreateBuilder(args);

System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
@"D:\School\Repositories\ProgrammingForDCloudHBA\Code\SWD63AMovieUploader\SWD63AMovieUploader\swd63aprogrammingforthecloud-ba30695f338b.json");

builder.Services.AddControllers();

string projectId = configuration["project"];
string secretId = configuration["secret"];

// Create the client.
SecretManagerServiceClient client = SecretManagerServiceClient.Create();

// Build the resource name.
SecretVersionName secretVersionName = new SecretVersionName(projectId, secretId, "1");

// Call the API.
AccessSecretVersionResponse result = client.AccessSecretVersion(secretVersionName);

// Convert the payload to a string. Payloads are bytes by default.
String secretKey = result.Payload.Data.ToStringUtf8();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle(options =>
    {
        options.ClientId = "513523861026-j1vt7i7a5u2ufd52js9ceipfgefdquae.apps.googleusercontent.com";
        options.ClientSecret = secretKey;
    });

builder.Services.AddScoped(provider => new FireStoreMovieRepository(projectId));

builder.Services.AddScoped(provider => new PubsubTranscriberRepository(projectId));

builder.Services.AddRazorPages();

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
