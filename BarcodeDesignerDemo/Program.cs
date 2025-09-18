using BarcodeDesignerDemo.Data.Contaract;
using BarcodeDesignerDemo.Data.DbContextFile;
using BarcodeDesignerDemo.Requests.Handler;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()     // allow any domain (use .WithOrigins("http://localhost:3000") for specific frontend)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<BarcodeDbContext>(x =>
    x.UseSqlServer(builder.Configuration.GetConnectionString("DbConnection")));
builder.Services.AddScoped<IBarcodeDbContext, BarcodeDbContext>();
builder.Services.AddScoped<GeneratePriceTagPdfHandler>();
builder.Services.AddScoped<GenerateTagNewPdfHandler>();
builder.Services.AddScoped<GenerateCSVPDFHandler>();
builder.Services.AddMediatR(x => x.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
var app = builder.Build();

//using (var scope = app.Services.CreateScope())
//{
//    var contextDb = scope.ServiceProvider.GetRequiredService<BarcodeDbContext>();
//    contextDb.Database.Migrate();
//}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

EnsureDatabaseAndTableExists();
app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


void EnsureDatabaseAndTableExists()
{
    var connectionString = builder.Configuration.GetConnectionString("DbConnection");
    var builderConn = new SqlConnectionStringBuilder(connectionString);
    string databaseName = builderConn.InitialCatalog;

    // Step 1: Connect to master and create database if it doesn't exist
    builderConn.InitialCatalog = "master"; // connect to master
    using (var connection = new SqlConnection(builderConn.ConnectionString))
    {
        connection.Open();
        string createDbScript = $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                CREATE DATABASE [{databaseName}];
            END";
        using var command = new SqlCommand(createDbScript, connection);
        command.ExecuteNonQuery();
        Console.WriteLine($"Database '{databaseName}' checked/created.");
    }

    // Step 2: Connect to the new database and create table if it doesn't exist
    builderConn.InitialCatalog = databaseName; // now connect to the actual DB
    using (var connection = new SqlConnection(builderConn.ConnectionString))
    {
        connection.Open();
        string createTableScript = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LabelTemplates')
            BEGIN
                CREATE TABLE LabelTemplates (
                  [Id] [int] IDENTITY(1,1) NOT NULL,
	                [Name] [nvarchar](100) NOT NULL,
	                [TemplateJson] [nvarchar](max) NOT NULL,
	                [CreatedDate] [datetime] NULL,
                );
            END";
        using var command = new SqlCommand(createTableScript, connection);
        command.ExecuteNonQuery();
        Console.WriteLine("Table 'LabelTemplates' checked/created.");
    }
}


