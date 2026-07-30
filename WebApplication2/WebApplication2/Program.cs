using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using WebApplication2.Data;
using WebApplication2.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddTransient<TodoRepository>();
builder.Services.AddTransient<UserRepository>();

// Configure JWT authentication
var jwt = builder.Configuration.GetSection("Jwt");
var key = jwt.GetValue<string>("Key") ?? throw new InvalidOperationException("Jwt:Key must be set");
var issuer = jwt.GetValue<string>("Issuer");
var audience = jwt.GetValue<string>("Audience");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

var app = builder.Build();

// Initialize database and seed sample data
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    using var conn = factory.CreateConnection();
    conn.Open();
    // create table if not exists (MySQL)
    conn.Execute(@"CREATE TABLE IF NOT EXISTS Todos (
        Id INT AUTO_INCREMENT PRIMARY KEY,
        Title VARCHAR(200) NOT NULL,
        IsCompleted TINYINT(1) NOT NULL DEFAULT 0
    );");

    // create users table
    conn.Execute(@"CREATE TABLE IF NOT EXISTS Users (
        Id INT AUTO_INCREMENT PRIMARY KEY,
        Username VARCHAR(100) NOT NULL UNIQUE,
        PasswordHash VARCHAR(200) NOT NULL
    );");

    var usersCount = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM Users");
    if (usersCount == 0)
    {
        // seed a demo user: username=admin password=P@ssw0rd!
        var pwd = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!");
        conn.Execute("INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)", new[] {
            new { Username = "admin", PasswordHash = pwd }
        });
    }

    // If the database contains a plain 'Password' column (legacy), migrate plaintext passwords to PasswordHash
    var hasPlainPasswordColumn = conn.ExecuteScalar<int>(@"SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'Password'");

    if (hasPlainPasswordColumn > 0)
    {
        // Ensure PasswordHash column exists
        var hasPasswordHashColumn = conn.ExecuteScalar<int>(@"SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'PasswordHash'");

        if (hasPasswordHashColumn == 0)
        {
            conn.Execute("ALTER TABLE Users ADD COLUMN PasswordHash VARCHAR(200)");
        }

        // Migrate rows where Password is present and PasswordHash is empty
        var rows = conn.Query<(int Id, string Username, string Password)>("SELECT Id, Username, Password FROM Users WHERE Password IS NOT NULL AND (PasswordHash IS NULL OR PasswordHash = '')");
        foreach (var row in rows)
        {
            try
            {
                var hashed = BCrypt.Net.BCrypt.HashPassword(row.Password);
                conn.Execute("UPDATE Users SET PasswordHash = @Hash WHERE Id = @Id", new { Hash = hashed, Id = row.Id });
                // optionally clear plain password
                conn.Execute("UPDATE Users SET Password = NULL WHERE Id = @Id", new { Id = row.Id });
            }
            catch (Exception)
            {
                // ignore hashing errors for individual rows; they'll remain un-migrated
            }
        }
    }

    var count = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM Todos");
    if (count == 0)
    {
        conn.Execute("INSERT INTO Todos (Title, IsCompleted) VALUES (@Title, @IsCompleted)", new[] {
            new { Title = "Buy milk", IsCompleted = 0 },
            new { Title = "Walk the dog", IsCompleted = 1 },
            new { Title = "Write code", IsCompleted = 0 }
        });
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   // app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApplication2 v1");
        options.RoutePrefix = "swagger"; // serve at /swagger
    });
}

// Add custom middleware
app.UseMiddleware<WebApplication2.Middleware.ExceptionHandlingMiddleware>();
app.UseMiddleware<WebApplication2.Middleware.RequestResponseLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
