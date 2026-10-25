namespace TicTacToe.Api;

public sealed class GameStore
{
    private static readonly int[][] WinningLines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6]
    ];

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Game> _games = [];
    private readonly Scoreboard _scoreboard = new();

    public GameState Create(string mode)
    {
        lock (_gate)
        {
            var game = new Game(Guid.NewGuid(), mode);
            _games.Add(game.Id, game);
            return Snapshot(game);
        }
    }

    public GameState? Get(Guid id)
    {
        lock (_gate)
            return _games.TryGetValue(id, out var game) ? Snapshot(game) : null;
    }

    public GameOperationResult Play(Guid id, int cell)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(id, out var game))
                return GameOperationResult.Missing();
            if (cell is < 0 or > 8)
                return GameOperationResult.Invalid("Cell must be between 0 and 8.");
            if (game.Status != "inProgress")
                return GameOperationResult.Invalid("This game has already ended.");
            if (game.Board[cell] is not null)
                return GameOperationResult.Invalid("That cell is already taken.");
            if (game.Mode == "computer" && game.CurrentPlayer != "X")
                return GameOperationResult.Invalid("The computer makes the O moves.");

            ApplyMove(game, cell);
            if (game.Mode == "computer" && game.Status == "inProgress")
                ApplyMove(game, ChooseComputerMove(game.Board));

            return GameOperationResult.Success(Snapshot(game));
        }
    }

    public GameOperationResult Undo(Guid id)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(id, out var game))
                return GameOperationResult.Missing();
            if (game.Moves.Count == 0)
                return GameOperationResult.Invalid("There are no moves to undo.");

            if (game.ScoreCounted && game.Status == "won")
            {
                if (game.Winner == "X") _scoreboard.XWins--;
                else _scoreboard.OWins--;
            }
            else if (game.ScoreCounted && game.Status == "draw")
            {
                _scoreboard.Draws--;
            }
            game.ScoreCounted = false;

            var removeCount = 1;
            if (game.Mode == "computer" &&
                game.Moves[^1].Player == "O" &&
                game.Moves.Count > 1 &&
                game.Moves[^2].Player == "X")
            {
                removeCount = 2;
            }

            for (var i = 0; i < removeCount; i++)
            {
                var lastMove = game.Moves[^1];
                game.Board[(lastMove.Row - 1) * 3 + lastMove.Column - 1] = null;
                game.Moves.RemoveAt(game.Moves.Count - 1);
            }

            game.Status = "inProgress";
            game.Winner = null;
            game.WinningCells = [];
            game.CurrentPlayer = game.Moves.Count == 0 || game.Moves[^1].Player == "O" ? "X" : "O";
            return GameOperationResult.Success(Snapshot(game));
        }
    }

    public GameOperationResult Reset(Guid id)
    {
        lock (_gate)
        {
            if (!_games.Remove(id, out var previous))
                return GameOperationResult.Missing();
            var freshGame = new Game(Guid.NewGuid(), previous.Mode);
            _games.Add(freshGame.Id, freshGame);
            return GameOperationResult.Success(Snapshot(freshGame));
        }
    }

    public Scoreboard GetScoreboard()
    {
        lock (_gate)
            return _scoreboard with { };
    }

    public Scoreboard ResetScoreboard()
    {
        lock (_gate)
        {
            _scoreboard.XWins = 0;
            _scoreboard.OWins = 0;
            _scoreboard.Draws = 0;
            foreach (var game in _games.Values)
                game.ScoreCounted = false;
            return _scoreboard with { };
        }
    }

    private void ApplyMove(Game game, int cell)
    {
        var player = game.CurrentPlayer;
        game.Board[cell] = player;
        game.Moves.Add(new MoveState(game.Moves.Count + 1, player, cell / 3 + 1, cell % 3 + 1));

        var winningLine = FindWinningLine(game.Board, player);
        if (winningLine is not null)
        {
            game.Status = "won";
            game.Winner = player;
            game.WinningCells = winningLine;
            if (player == "X") _scoreboard.XWins++;
            else _scoreboard.OWins++;
            game.ScoreCounted = true;
            return;
        }

        if (game.Board.All(value => value is not null))
        {
            game.Status = "draw";
            _scoreboard.Draws++;
            game.ScoreCounted = true;
            return;
        }

        game.CurrentPlayer = player == "X" ? "O" : "X";
    }

    private static int ChooseComputerMove(string?[] board)
    {
        foreach (var player in new[] { "O", "X" })
        {
            foreach (var cell in Enumerable.Range(0, 9).Where(index => board[index] is null))
            {
                board[cell] = player;
                var canCompleteLine = FindWinningLine(board, player) is not null;
                board[cell] = null;
                if (canCompleteLine) return cell;
            }
        }

        if (board[4] is null) return 4;
        foreach (var corner in new[] { 0, 2, 6, 8 })
            if (board[corner] is null) return corner;
        return Array.FindIndex(board, value => value is null);
    }

    private static int[]? FindWinningLine(string?[] board, string player) =>
        WinningLines.FirstOrDefault(line => line.All(cell => board[cell] == player));

    private static GameState Snapshot(Game game) =>
        new(
            game.Id,
            game.Mode,
            (string?[])game.Board.Clone(),
            game.CurrentPlayer,
            game.Moves.ToArray(),
            game.Status,
            game.Winner,
            game.WinningCells.ToArray());

    private sealed class Game(Guid id, string mode)
    {
        public Guid Id { get; } = id;
        public string Mode { get; } = mode;
        public string?[] Board { get; } = new string?[9];
        public string CurrentPlayer { get; set; } = "X";
        public List<MoveState> Moves { get; } = [];
        public string Status { get; set; } = "inProgress";
        public string? Winner { get; set; }
        public int[] WinningCells { get; set; } = [];
        public bool ScoreCounted { get; set; }
    }
}

public sealed record MoveState(int MoveNumber, string Player, int Row, int Column);
public sealed record GameState(
    Guid Id,
    string Mode,
    string?[] Board,
    string CurrentPlayer,
    IReadOnlyList<MoveState> Moves,
    string Status,
    string? Winner,
    IReadOnlyList<int> WinningCells);
public sealed record GameOperationResult(GameState? State, string? Error, bool NotFound = false)
{
    public static GameOperationResult Success(GameState state) => new(state, null);
    public static GameOperationResult Invalid(string error) => new(null, error);
    public static GameOperationResult Missing() => new(null, "Game not found.", true);
}
public sealed record Scoreboard
{
    public int XWins { get; set; }
    public int OWins { get; set; }
    public int Draws { get; set; }
}
