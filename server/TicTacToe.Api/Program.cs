using TicTacToe.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GameStore>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapPost("/api/games", (CreateGameRequest request, GameStore games) =>
{
    if (request.Mode is not ("twoPlayer" or "computer"))
        return Results.Problem("Mode must be 'twoPlayer' or 'computer'.", statusCode: StatusCodes.Status400BadRequest);

    var game = games.Create(request.Mode);
    return Results.Created($"/api/games/{game.Id}", game);
});

app.MapGet("/api/games/{id:guid}", (Guid id, GameStore games) =>
    games.Get(id) is { } game ? Results.Ok(game) : Results.NotFound());

app.MapPost("/api/games/{id:guid}/moves", (Guid id, PlayMoveRequest request, GameStore games) =>
    ToHttpResult(games.Play(id, request.Cell)));

app.MapPost("/api/games/{id:guid}/undo", (Guid id, GameStore games) =>
    ToHttpResult(games.Undo(id)));

app.MapPost("/api/games/{id:guid}/reset", (Guid id, GameStore games) =>
    ToHttpResult(games.Reset(id)));

app.MapGet("/api/scoreboard", (GameStore games) => Results.Ok(games.GetScoreboard()));

app.MapPost("/api/scoreboard/reset", (GameStore games) =>
    Results.Ok(games.ResetScoreboard()));

app.Run();

static IResult ToHttpResult(GameOperationResult result)
{
    if (result.State is not null) return Results.Ok(result.State);
    return Results.Problem(
        result.Error,
        statusCode: result.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
}

public sealed record CreateGameRequest(string Mode);
public sealed record PlayMoveRequest(int Cell);
