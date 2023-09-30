using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.File;
using System.Diagnostics;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// follow the tutorial here
//https://www.elastic.co/blog/getting-started-with-the-elastic-stack-and-docker-compose
//https://github.com/elkninja
//https://github.com/elkninja/elastic-stack-docker-part-one

//add logger
Log.Logger = ConfigureLogs(builder);
builder.Host.UseSerilog();
Log.Logger.Information("Application Starting.");

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//use logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

#region helper
Serilog.ILogger ConfigureLogs(WebApplicationBuilder builder)
{
    //get the environment in which the application is running for
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", false, true)
        .AddJsonFile($"appsettings.{env}.json", true, true)
        .AddEnvironmentVariables()
        .Build();

    // create logger
    var loggerConfig = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .Enrich.WithExceptionDetails() //add details of the exception
        .WriteTo.Debug()
        .WriteTo.Console()
        .WriteTo.Elasticsearch(ConfigureELSSink(config, env))
        .Enrich.WithProperty("Environment", env);

    //Log.Logger = new LoggerConfiguration()
    //.MinimumLevel.Debug()
    //.WriteTo.Console()
    //.CreateLogger();

    Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
    Serilog.Debugging.SelfLog.Enable(Console.Error);

    return loggerConfig.CreateLogger();
}

ElasticsearchSinkOptions ConfigureELSSink(IConfigurationRoot config, string env)
{
    var uri = config.GetValue<string>("ELKConfiguration:Uri"); 
    Debug.WriteLine($"***************{uri}");
    Debug.WriteLine($"+++++++++++++++{Assembly.GetExecutingAssembly().GetName().Name?.ToLower()}");
    return new ElasticsearchSinkOptions(new Uri(uri))
    {
        AutoRegisterTemplate = true,
        //IndexFormat = $"sampleapi-{DateTime.UtcNow:yyyy-MM}",
        IndexFormat = $"{Assembly.GetExecutingAssembly().GetName().Name?.ToLower()}-{env.ToLower().Replace(".","-")}-{DateTime.UtcNow:yyyy-MM}",
        NumberOfReplicas = 1,
        NumberOfShards = 2,
        MinimumLogEventLevel = Serilog.Events.LogEventLevel.Debug,
        FailureCallback = e => Console.WriteLine("Unable to submit event " + e.MessageTemplate),
        EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                       EmitEventFailureHandling.WriteToFailureSink |
                                       EmitEventFailureHandling.RaiseCallback
    };
}
#endregion