# little XO

A browser-based Tic-Tac-Toe game built with Angular and a .NET 10 REST API. The API owns game sessions, move validation, move history, computer turns, undo behavior, and the session scoreboard. All game and scoreboard data is held in memory and resets when the API process stops.

## Submission contents

- `client/` — Angular and TypeScript frontend source, styles, and frontend tests.
- `server/TicTacToe.Api/` — .NET 10 minimal API and in-memory game logic.
- `INTERVIEW_GUIDE.md` — code walkthrough and interview preparation notes.
- This README — setup, test, API, AI workflow, and assumptions/limitations.

## Requirements

- Node.js 20.19+ or 22.12+
- .NET 10 SDK

## Run locally

From the repository root, start the API in one terminal:

```powershell
dotnet run --project server\TicTacToe.Api --urls http://localhost:5080
```

From the repository root, start Angular in a second terminal:

```powershell
Set-Location .\client
npm install
npm start
```

Open [http://localhost:4200](http://localhost:4200). The API allows requests from that local Angular origin.

To build the backend without running it:

```powershell
dotnet build server\TicTacToe.Api
```

## Play

- Choose **Two players** or **Play computer**. X goes first; in computer mode X is human-controlled and O is the computer.
- Select an empty square to play. The computer takes its turn immediately after X.
- **Undo last move** removes one move in two-player mode, or the latest human/computer pair in computer mode. If X ends a computer-mode game before O can reply, undo removes X's winning move.
- **New game** clears the current board and history without changing the scoreboard.
- **Reset scoreboard** clears all session scores.

The computer tries a winning move, blocks an immediate X win, takes the center, takes a corner, then chooses any remaining square.

## API documentation

The API listens on `http://localhost:5080`. Board cell indices are zero-based (top-left is `0`, bottom-right is `8`); move-history rows and columns are one-based for display.

| Method | Endpoint | Request | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/games` | `{ "mode": "twoPlayer" }` or `{ "mode": "computer" }` | Create a session; returns its initial state |
| `GET` | `/api/games/{id}` | — | Get the current game state |
| `POST` | `/api/games/{id}/moves` | `{ "cell": 0 }` | Submit X's or the current player's move; computer mode also returns O's automatic move |
| `POST` | `/api/games/{id}/undo` | `{}` | Undo the last move or computer-mode move pair |
| `POST` | `/api/games/{id}/reset` | `{}` | Start a new session in the same mode; returns the new session ID |
| `GET` | `/api/scoreboard` | — | Get X wins, O wins, and draws |
| `POST` | `/api/scoreboard/reset` | `{}` | Reset all scoreboard counts |

Game responses include `id`, `mode`, `board`, `currentPlayer`, `moves`, `status`, `winner`, and `winningCells`. Invalid moves return HTTP `400`; unknown game IDs return HTTP `404`.

## Test instructions

Run the Angular tests once in Chrome and build the production frontend:

```powershell
Set-Location .\client
npx ng test --watch=false
npm run build
```

Build the backend from the repository root:

```powershell
dotnet build server\TicTacToe.Api
```

There is currently no automated .NET test project. For a quick API smoke test, start the API as described above, then run these commands from another PowerShell terminal:

```powershell
$base = 'http://localhost:5080/api'
$game = Invoke-RestMethod -Method Post -Uri "$base/games" -ContentType 'application/json' -Body '{"mode":"computer"}'
Invoke-RestMethod -Method Post -Uri "$base/games/$($game.id)/moves" -ContentType 'application/json' -Body '{"cell":0}'
Invoke-RestMethod -Method Post -Uri "$base/games/$($game.id)/undo" -ContentType 'application/json' -Body '{}'
Invoke-RestMethod -Uri "$base/scoreboard"
```

The create and move responses should show the game state and the computer's reply; undo should return an empty board, and the scoreboard endpoint should return its three counts.

## AI workflow notes

AI assistance was used as a development aid to scaffold the Angular app, draft the UI and REST wiring, implement the in-memory game/session rules, and prepare the interview guide and documentation. The implementation was then reviewed against the problem statement and checked with the Angular production build, frontend tests, .NET build, and a live API smoke test covering game creation, a computer reply, undo, and scoreboard retrieval.

The core design choices reflected in the code are:

- Keep authoritative game rules and scoreboard updates on the API; let Angular render returned state.
- Use a singleton in-memory store to satisfy the brief without adding database setup.
- Represent supported players, game modes, and game statuses as explicit TypeScript types.
- Keep the computer strategy simple and follow the priority specified in the problem statement.

## Assumptions and limitations

- In-memory storage is intentional and allowed by the problem statement. Games and scores are lost when the API process restarts.
- The API is intended to run as one local process. Separate API instances would have separate game and scoreboard state.
- CORS is configured for the Angular development origin `http://localhost:4200`; it must be changed for a different frontend host.
- The local API uses HTTP and has no authentication or authorization. It is a development/demo application, not a production deployment.
- The computer opponent follows the specified tactical priority; it does not use minimax and is not guaranteed to play optimally.
- Automated tests currently cover the Angular component and its API request contract. Backend logic has been build-checked and smoke-tested, but does not yet have an automated .NET test project.
