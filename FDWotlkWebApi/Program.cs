using FDWotlkWebApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Add services to the container.
builder.Services.AddScoped<ISoapAccountProvisioner, SoapSoapAccountProvisioner>();
// Register DB service
builder.Services.AddScoped<IMySqlService, MySqlService>();
// Register SOAP account provisioner
builder.Services.Configure<SoapServerOptions>(builder.Configuration.GetSection("SoapServer"));
// Register HttpClient for SoapAccountProvisioner
builder.Services.AddHttpClient<SoapSoapAccountProvisioner>();
// Add MySqlService configuration to use appsettings
builder.Services.Configure<MySqlOptions>(builder.Configuration.GetSection("ConnectionStrings"));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        corsPolicyBuilder => corsPolicyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    
    // Automatically redirect to Scalar documentation
    app.MapGet("/", () => Results.Redirect("/scalar"));
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

