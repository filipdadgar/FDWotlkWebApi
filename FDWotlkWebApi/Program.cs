using FDWotlkWebApi.Services;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IAccountProvisioner>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SoapServerOptions>>().Value;
    return new SoapAccountProvisioner(options.Host, options.Port);
});
builder.Services.AddControllers();
// Register DB service
builder.Services.AddScoped<IMySqlService, MySqlService>();

// Register SOAP account provisioner
builder.Services.Configure<SoapServerOptions>(builder.Configuration.GetSection("SoapServer"));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    
    // Automatically redirect to Scalar documentation
    app.MapGet("/", () => Results.Redirect("/scalar"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

