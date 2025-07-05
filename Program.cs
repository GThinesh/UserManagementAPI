
using Microsoft.OpenApi.Models;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory user store
var users = new List<User>();


var app = builder.Build();


// Exception handling middleware (should be first)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = new { error = "Internal server error." };
        var json = System.Text.Json.JsonSerializer.Serialize(error);
        await context.Response.WriteAsync(json);
        // Optionally log the exception
        Console.WriteLine($"Exception: {ex.Message}");
    }
});

// Token validation middleware (should be second)
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        var error = new { error = "Unauthorized: Missing or invalid token." };
        var json = System.Text.Json.JsonSerializer.Serialize(error);
        await context.Response.WriteAsync(json);
        return;
    }
    var token = authHeader.Substring("Bearer ".Length).Trim();
    if (token != "mysecrettoken")
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        var error = new { error = "Unauthorized: Invalid token." };
        var json = System.Text.Json.JsonSerializer.Serialize(error);
        await context.Response.WriteAsync(json);
        return;
    }
    await next();
});

// Logging middleware (should be last)
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;
    await next();
    var statusCode = context.Response.StatusCode;
    Console.WriteLine($"{method} {path} => {statusCode}");
});

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CRUD Endpoints
/// <summary>
/// Retrieves all users.
/// </summary>
app.MapGet("/users", () => users);

/// <summary>
/// Retrieves a user by their ID.
/// </summary>
app.MapGet("/users/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

/// <summary>
/// Creates a new user. Requires a unique email and a non-empty name.
/// </summary>
app.MapPost("/users", (User user) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(user.Name))
            return Results.BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
            return Results.BadRequest("A valid email is required.");
        if (users.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest("Email already exists.");

        user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
        users.Add(user);
        return Results.Created($"/users/{user.Id}", user);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
});

/// <summary>
/// Updates an existing user's details by ID. Requires a unique email and a non-empty name.
/// </summary>
app.MapPut("/users/{id}", (int id, User updatedUser) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(updatedUser.Name))
            return Results.BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(updatedUser.Email) || !updatedUser.Email.Contains("@"))
            return Results.BadRequest("A valid email is required.");
        if (users.Any(u => u.Email.Equals(updatedUser.Email, StringComparison.OrdinalIgnoreCase) && u.Id != id))
            return Results.BadRequest("Email already exists.");

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;
        return Results.Ok(user);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
});

/// <summary>
/// Deletes a user by their ID.
/// </summary>
app.MapDelete("/users/{id}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound();
        users.Remove(user);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}");
    }
});

app.Run();

// User model
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
 
