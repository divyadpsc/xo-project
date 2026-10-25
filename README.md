# little XO

A browser-based Tic-Tac-Toe game built with Angular and a .NET 10 REST API. The API owns game sessions, move validation, move history, computer turns, undo behavior, and the session scoreboard. All game and scoreboard data is held in memory and resets when the API process stops.

## Requirements

- Node.js 20.19+ or 22.12+
- .NET 10 SDK

## Run locally

Start the API in one terminal:

```powershell
dotnet run --project server\TicTacToe.Api --urls http://localhost:5080
```

Start Angular in another terminal:

```powershell
cd client
npm install
npm start
```

Open [http://localhost:4200](http://localhost:4200). The API allows requests from that local Angular origin.

## Play

- Choose **Two players** or **Play computer**. X goes first; in computer mode X is human-controlled and O is the computer.
- Select an empty square to play. The computer takes its turn immediately after X.
- **Undo last move** removes one move in two-player mode, or the latest human/computer pair in computer mode. If X ends a computer-mode game before O can reply, undo removes X's winning move.
- **New game** clears the current board and history without changing the scoreboard.
- **Reset scoreboard** clears all session scores.

The computer tries a winning move, blocks an immediate X win, takes the center, takes a corner, then chooses any remaining square.

## REST API

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

## Verify

```powershell
cd client
npx ng test --watch=false
npm run build
```

Build or run the backend with the .NET 10 SDK:

```powershell
dotnet build server\TicTacToe.Api
```
