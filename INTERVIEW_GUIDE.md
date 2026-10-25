# little XO — Interview and Code Walkthrough

This guide explains the handwritten application code by file and by logical line range. Blank lines and braces are explained with the statement or block they belong to; they do not add runtime behavior. Generated files, installed packages, and build output are intentionally excluded.

## 1. A short interview introduction

> “I built a browser Tic-Tac-Toe game with an Angular standalone frontend and a .NET minimal-API backend. The backend is the source of truth: it owns each game session, validates moves, detects wins and draws, runs the computer strategy, applies undo rules, and updates the session scoreboard. Angular renders the server-returned state and calls the backend through a small typed REST service. I used in-memory storage because the brief allows it, so state resets when the API process stops.”

## 2. Architecture and a typical move

1. Angular bootstraps `App` and provides `HttpClient`.
2. `App.ngOnInit()` requests the scoreboard and creates a game.
3. `GameApiService` turns a user action into an HTTP request.
4. A minimal API endpoint in `Program.cs` delegates the operation to the singleton `GameStore`.
5. `GameStore` validates and updates state while holding a lock, then returns a snapshot.
6. ASP.NET serializes the response as JSON. Angular stores the response in a signal and the template updates automatically.

The browser never decides whether a move wins. That rule runs on the backend, which prevents the display and game rules from becoming competing sources of truth.

## 3. Backend

### `server/TicTacToe.Api/Program.cs`

**Lines 1–3 — namespace import and host creation**

- `using TicTacToe.Api;` makes the request records and `GameStore` visible to the top-level program.
- `WebApplication.CreateBuilder(args)` creates the ASP.NET Core host builder and reads standard command-line and environment configuration.

**Lines 5–12 — dependency injection and CORS**

- `AddSingleton<GameStore>()` registers one store instance for the lifetime of this API process. Games and scoreboard are consequently shared by requests to this process.
- `AddCors` registers a default cross-origin policy. It allows the Angular development server at `http://localhost:4200` to call the API, and allows HTTP methods and headers.
- In production, the allowed origin should be set to the deployed frontend rather than left as a development origin.

**Lines 14–15 — construct and configure the HTTP pipeline**

- `builder.Build()` creates the runnable web application from the builder and registered services.
- `UseCors()` applies the registered CORS policy to requests.

**Lines 17–24 — create-game endpoint**

- `MapPost("/api/games", ...)` defines the REST endpoint that creates a session.
- ASP.NET binds the JSON request body to `CreateGameRequest` and resolves `GameStore` from dependency injection.
- The mode guard accepts only `twoPlayer` and `computer`. An unsupported value returns HTTP 400 rather than creating an invalid session.
- A valid request calls `games.Create(mode)`.
- `Results.Created` returns HTTP 201, the new resource URL, and the initial game state.

**Lines 26–27 — get-game endpoint**

- `MapGet` defines `GET /api/games/{id}`.
- The `:guid` route constraint means the route parameter must parse as a GUID.
- `games.Get(id)` returns a snapshot when the game exists; the conditional expression maps it to HTTP 200 or HTTP 404.

**Lines 29–36 — move, undo, and reset endpoints**

- `POST /api/games/{id}/moves` binds a `PlayMoveRequest` and passes its cell index to `GameStore.Play`.
- `POST /api/games/{id}/undo` delegates mode-aware undo to `GameStore.Undo`.
- `POST /api/games/{id}/reset` delegates creation of a fresh session in the same mode to `GameStore.Reset`.
- All three pass the store result through `ToHttpResult`, keeping the endpoint declarations short and response mapping consistent.

**Lines 38–41 — scoreboard endpoints**

- `GET /api/scoreboard` returns the current session-level counts.
- `POST /api/scoreboard/reset` resets the counts and returns the new scoreboard.

**Line 43 — start the server**

- `app.Run()` starts listening for HTTP requests. Without it, the host is configured but does not serve the API.

**Lines 45–51 — shared operation-result mapping**

- `ToHttpResult` converts the store’s result into an HTTP result.
- If a state exists, it returns HTTP 200 with that state.
- Otherwise, it returns an RFC-style problem response. A missing game maps to HTTP 404; other rejected operations map to HTTP 400.

**Lines 53–54 — request DTOs**

- `CreateGameRequest` describes the JSON field expected when creating a game: `mode`.
- `PlayMoveRequest` describes the JSON field expected for a move: `cell`.
- Records are concise immutable request objects that ASP.NET can bind from JSON.

### `server/TicTacToe.Api/GameStore.cs`

**Lines 1–14 — store, win patterns, and shared state**

- The namespace identifies the backend module.
- `WinningLines` lists all eight three-cell winning combinations: three rows, three columns, and two diagonals. Board indices are zero-based.
- `_gate` is a shared lock protecting all in-memory game and score mutations.
- `_games` maps a session GUID to its private mutable game object.
- `_scoreboard` holds the process-wide X wins, O wins, and draws.
- The fields are private so callers must use the store methods and cannot mutate session internals directly.

**Lines 16–24 — `Create`**

- `Create(mode)` enters the lock, creates a game with a new `Guid`, inserts it into the dictionary, and returns a snapshot.
- Returning a snapshot rather than the internal `Game` prevents callers from holding a mutable reference to server state.

**Lines 26–30 — `Get`**

- `Get(id)` also uses the lock while reading the dictionary.
- `TryGetValue` avoids a separate existence check and lookup.
- The nullable return type means “not found” is represented as `null`; the endpoint maps that to HTTP 404.

**Lines 32–53 — `Play`**

- The method takes the lock so validation, the human move, a possible computer reply, and score changes form one serialized operation.
- It rejects an unknown game, an out-of-range cell, an already-finished game, an occupied cell, and a direct attempt to submit O’s move in computer mode.
- The checks happen before indexing the board, so invalid input cannot access an invalid array position.
- `ApplyMove` records the requested move and evaluates the board.
- In computer mode, the server immediately chooses and applies O’s reply only if the human move did not already finish the game.
- The returned `GameState` is a snapshot of the completed request, including both moves in computer mode.

**Lines 55–97 — `Undo`**

- The method rejects missing games and games with no recorded moves.
- If the game’s completion was counted on the scoreboard, undo subtracts that win or draw. `ScoreCounted` prevents decrementing a score that was reset separately.
- It defaults to removing one move. In computer mode, if the latest move is O and the previous move is X, it removes both as a human/computer pair.
- The loop removes moves newest-first. It translates the one-based history row and column back into a zero-based board index and empties that cell.
- It clears the completed status, winner, and winning-line highlight, then recalculates the player to move from the last remaining move.
- Finally, it returns the restored state as a snapshot.

**Lines 99–111 — `Reset`**

- `Reset(id)` removes the existing game and returns “not found” if the identifier is invalid.
- It creates a fresh game in the previous game’s mode. The new session gets a new ID, an empty board, no move history, and X as the first player.
- The scoreboard is intentionally not changed.

**Lines 113–120 — read and reset scoreboard**

- `GetScoreboard` returns a record copy, not the mutable shared scoreboard itself.
- `ResetScoreboard` zeros each counter under the lock.
- It also clears `ScoreCounted` on active game objects. If one of those games later has its completed result undone, the earlier result will not incorrectly subtract from the newly reset scoreboard.

**Lines 122–153 — `ApplyMove`**

- It saves the current player, writes that symbol to the board, and appends a history item.
- Move number is history length plus one. The cell index is converted to human-readable one-based row and column using integer division and remainder.
- It asks `FindWinningLine` whether this player now owns a full line. If so, it records the winner/status/highlight, increments exactly one scoreboard counter, marks the result as counted, and returns.
- Otherwise it checks whether every cell is occupied. If so, it marks a draw, increments draws once, marks that result as counted, and returns.
- If the game continues, it switches the current player between X and O.

**Lines 155–176 — `ChooseComputerMove`**

- It first examines O and then X. For each empty cell, it temporarily places the examined symbol and checks whether that move completes a line.
- It restores the temporary cell before deciding what to do next. This is a simple “try a move, inspect, undo the simulation” strategy.
- Returning O’s winning move implements the computer-win priority; returning X’s winning move blocks the human.
- If neither has an immediate winning line, it chooses the center, then the first available corner, then the first remaining empty cell.
- The computer strategy is intentionally rule-based rather than a minimax search because the requirements specify this priority order.

**Lines 178–181 — winning-line detection**

- `FindWinningLine` checks each predefined winning triple.
- `All` ensures all three board entries equal the requested player.
- It returns the matching indices, or `null` if no line exists. The same helper is used for real moves and computer simulations.

**Lines 183–194 — `Snapshot`**

- `Snapshot` builds the API-facing immutable-shaped state object from the mutable internal game.
- It clones the board and winning-cell arrays and copies the history list to an array. This stops a response consumer from modifying the store through shared collection references.

**Lines 196–204 — private `Game` state**

- This nested class is the mutable representation held only inside `GameStore`.
- Its primary constructor takes the ID and mode.
- A new game has nine empty cells, X to move, an empty move list, `inProgress` status, no winner, no winning cells, and no counted scoreboard result.

**Lines 206–227 — response and result records**

- `MoveState` is one history row: sequential number, player, and one-based row/column.
- `GameState` is the public response shape: ID, mode, board, current player, move list, status, winner, and winning cells.
- `GameOperationResult` represents either a successful state or an error. `NotFound` distinguishes a missing game from an invalid operation.
- Its named factory methods (`Success`, `Invalid`, and `Missing`) make the intention of each result explicit at the call site.
- `Scoreboard` contains the three session counters. It is a record with setters because the store updates the one shared instance.

### `server/TicTacToe.Api/TicTacToe.Api.csproj`

- `<Project Sdk="Microsoft.NET.Sdk.Web">` selects the ASP.NET Core web application SDK, which supplies web hosting and API defaults.
- `TargetFramework` selects .NET 10.
- `Nullable` enables nullable-reference analysis, making optional values such as “no winner yet” explicit in types.
- `ImplicitUsings` includes common .NET namespaces automatically, reducing boilerplate `using` directives.

## 4. Angular and TypeScript

### `client/src/app/game.models.ts`

- `Player` restricts a player value to `'X'` or `'O'`.
- `GameMode` restricts the mode to `'twoPlayer'` or `'computer'`.
- `GameStatus` restricts status to `'inProgress'`, `'won'`, or `'draw'`.
- `GameMove` defines a history entry and uses one-based row/column values for display.
- `GameState` mirrors the backend JSON contract. Its board permits a player mark or `null` for an empty cell; `winner` is nullable while the game is unfinished.
- `Scoreboard` describes the three count fields returned by the API.
- These are compile-time TypeScript types: they catch frontend mistakes but do not perform runtime validation of arbitrary JSON.

### `client/src/app/game-api.service.ts`

- The imports provide Angular’s HTTP client and injection mechanism, RxJS `Observable`, and the API data types.
- `@Injectable({ providedIn: 'root' })` lets Angular create one application-wide instance of this stateless service.
- `inject(HttpClient)` obtains the configured HTTP client.
- `baseUrl` centralizes the local API prefix so endpoint methods do not repeat it.
- `createGame` sends `POST /games` with a mode and types the response as `GameState`.
- `playMove` sends `POST /games/{id}/moves` with a cell index.
- `undo` and `resetGame` post to their respective game operations. The empty object makes the POST body explicit.
- `getScoreboard` reads the current counts with GET.
- `resetScoreboard` posts to the reset endpoint and types its response.
- Each method returns an Observable; the component subscribes when it needs the result. This keeps URLs and HTTP details out of the template.

### `client/src/app/app.ts`

**Lines 1–5 — imports**

- `HttpErrorResponse` identifies Angular HTTP failures so the UI can show a useful server-connection message.
- `Component`, `OnInit`, and `signal` provide the standalone component, initialization lifecycle, and reactive state.
- `Observable` and `finalize` support typed requests and cleanup after success or failure.
- The service and model imports connect the component to the REST layer and keep its state typed.

**Lines 7–12 — component metadata**

- `@Component` associates the class with the `app-root` selector.
- `templateUrl` and `styleUrl` keep markup and component styles in their own files.
- The empty `imports` array is sufficient because this template uses built-in Angular control flow and native HTML elements only.

**Lines 13–20 — component state and dependency injection**

- `App implements OnInit` means Angular calls `ngOnInit` after constructing the component.
- `game` begins as `null` while the API session is being created.
- `scoreboard` begins at zero so the display has a safe initial value.
- `mode` defaults to two-player mode.
- `busy` prevents overlapping user requests; `errorMessage` holds a message for the view.
- The constructor injects `GameApiService`.

**Lines 22–35 — initialization and mode**

- `ngOnInit` fetches the scoreboard and starts a new game.
- `startGame` calls the API for the selected mode and saves the returned session.
- `selectMode` avoids a redundant API call when the user selects the existing mode; otherwise it changes mode and creates a fresh game.

**Lines 37–53 — moves and undo**

- `play` reads the latest game and returns early if there is no session, a request is in flight, the game is complete, or the selected cell is occupied.
- It submits a valid selection to the API. On success, it replaces local game state with the authoritative response and refreshes the scoreboard.
- `undo` is guarded against missing sessions, concurrent actions, and empty history. On success it updates game state and refreshes scores in case undo reversed a result.

**Lines 55–64 — reset actions**

- `resetGame` asks the API to create the fresh same-mode session and replaces the current state. It does not reset the scoreboard.
- `resetScoreboard` is a separate action and updates only the scoreboard from the API response.
- Both actions are blocked while another request is running.

**Lines 66–85 — presentation helpers**

- `cellLabel` converts a zero-based cell index into one-based row and column values and includes its current mark. This gives assistive technology a useful label for every square.
- `trackMove` tells Angular to identify rendered history entries by move number, helping it update list DOM efficiently.
- `statusText` maps game status into human-readable turn, win, or draw copy.
- `playerName` labels O as “Computer” only in computer mode.

**Lines 87–92 — scoreboard refresh**

- `refreshScoreboard` subscribes to the GET request.
- The success callback writes the returned count into the scoreboard signal.
- The error callback displays the shared error message rather than silently hiding a failed request.

**Lines 94–101 — shared request lifecycle**

- `runRequest<T>` accepts any typed Observable and a callback for its successful value.
- Before subscribing, it marks the component busy and clears a stale error.
- `finalize` runs whether the request succeeds or fails, so the busy state is always released.
- The subscription calls the supplied success callback or routes the error to `showError`.

**Lines 103–114 — error display**

- `showError` handles `HttpErrorResponse` separately from other unexpected errors.
- If the API returned a problem `detail`, the UI displays it; otherwise, an HTTP/network failure gets a clear “start the .NET API” message.
- Non-HTTP failures get a generic retry message.

### `client/src/app/app.html`

**Lines 1–8 — app header**

- The main landmark wraps the page.
- The header contains a home/brand link and a local-session indicator.
- Decorative marks are hidden from screen readers with `aria-hidden`; the link has an accessible label.

**Lines 10–30 — game introduction and mode selection**

- The main game section is labelled for assistive technology and separates the play panel from the sidebar.
- The mode buttons bind their selected state and `aria-pressed` to the `mode` signal.
- While a request is in flight, mode buttons are disabled. Clicks call `selectMode` with the corresponding typed mode.

**Lines 32–58 — status and turn message**

- `@if` checks whether the server has returned a game; `currentGame` is a local template alias.
- Nested `@if`/`@else if` shows the winner, draw indicator, or current player.
- The live status region announces turn/result changes to assistive technology.
- `statusText` supplies the main status; the nested condition selects its supporting message.

**Lines 60–75 — board**

- `@for` creates one button for each of the server’s nine board entries.
- `$index` is the stable zero-based cell index used by `play`.
- Class bindings visually distinguish X, O, and winning cells.
- A cell is disabled while the request is running, when the game is complete, or when it is already occupied.
- `cellLabel` and `aria-pressed` expose cell state accessibly.
- Nested conditions render X, O, or a faint position number; clicking calls `play(index)`.

**Lines 77–84 — undo and new game controls**

- Undo is disabled when there is no move or an operation is in progress.
- New game is disabled only while busy; clicking calls the reset endpoint.

**Lines 85–100 — setup/error state**

- If no game exists, the template shows a setup message.
- If setup failed, it instead displays a clear server-unavailable message and a retry button.
- The retry calls `startGame`; the button is disabled while the retry is pending.

**Lines 102–123 — scoreboard**

- The scoreboard card labels its heading for screen readers.
- Its three counts bind directly to the scoreboard signal.
- The separate reset button invokes `resetScoreboard` and respects the busy state.

**Lines 125–151 — move history**

- The card shows the current move count when a game is available.
- If there are moves, an ordered list renders each move using `trackMove`.
- Each entry displays its sequence, mark, player name, and row/column.
- In computer mode, O entries receive a CPU label.
- An empty-history message is shown before the first move.

**Lines 153–162 — tips, errors, and footer**

- The tip card is static presentation.
- A request error is shown in an alert region when a game already exists.
- The page footer closes the main landmark.

### `client/src/app/app.css`

The component stylesheet is presentation only; game rules remain in TypeScript and on the API. Blank lines separate CSS rules.

- **Lines 1–8:** `:host` makes the component host a block. `.app-shell` sets a centered maximum-width page with horizontal gutters.
- **Lines 10–32:** header and brand layout; `.brand strong` applies the accent color to XO.
- **Lines 34–58:** the four-square brand mark uses a small CSS grid, with two cells styled as circles.
- **Lines 60–80:** common small uppercase labels and the live/status dots share typography and dot styling.
- **Lines 82–106:** two-column main layout, play-panel sizing, and eyebrow accent.
- **Lines 108–163:** heading, intro text, and decorative circular round stamp.
- **Lines 165–198:** mode-switch container, buttons, selected state, and icon color.
- **Lines 200–256:** status card, player token, X/O colors, status copy, and completed-game indicator.
- **Lines 258–333:** square 3×3 CSS grid, cells, hover/focus/disabled states, winning-cell highlight, marks, and hover-only position numbers.
- **Lines 335–390:** game-action alignment, shared button reset, undo styling, disabled states, and primary new-game button.
- **Lines 392–429:** sidebar/card layout, card headings, section labels, and trophy accent.
- **Lines 431–496:** scoreboard’s three-column grid, symbols, counts, and reset action.
- **Lines 498–513:** history card height/padding, heading alignment, and move-count badge.
- **Lines 515–578:** scrollable ordered history list, row separators, move number, player mark, description, and CPU badge.
- **Lines 580–629:** empty-history illustration/message and the history footer/live dot.
- **Lines 631–656:** tip-card layout, icon, paragraph, and emphasized label.
- **Lines 658–677:** square loading placeholder and animated loading dot.
- **Lines 679–708:** centered setup-error content and retry button styling.
- **Lines 710–718:** visible inline error banner styles.
- **Lines 720–728:** footer typography and separation.
- **Lines 730–737:** `pop-in` animates a newly rendered mark; `pulse` softly animates the loading indicator.
- **Lines 739–759:** at widths up to 760px, the layout becomes one main column and the sidebar can use two columns.
- **Lines 761–799:** at widths up to 500px, the sidebar becomes one column and spacing/type are adjusted for phones.
- **Lines 801–814:** reduced-motion users receive near-instant animations and transitions.

### `client/src/styles.css`

- `:root` defines the shared color tokens, font stack, light color scheme, and text rendering defaults.
- The universal selector uses `box-sizing: border-box` so declared dimensions include padding and borders.
- `body` establishes the minimum viewport size and removes default browser margin.
- `button` removes the tap highlight on touch devices.
- `::selection` gives selected text the app’s palette.

### `client/src/app/app.config.ts` and `client/src/main.ts`

- `app.config.ts` declares the providers used by the standalone app.
- `provideBrowserGlobalErrorListeners()` registers Angular’s browser-side global error listeners.
- `provideZoneChangeDetection({ eventCoalescing: true })` configures Zone.js change detection and coalesces related browser events.
- `provideHttpClient()` makes `HttpClient` available to `GameApiService`.
- `main.ts` imports Angular’s standalone bootstrap function, the app config, and the root component.
- `bootstrapApplication(App, appConfig)` starts the UI without an NgModule. The `catch` logs a bootstrap failure, when Angular cannot start at all.

### `client/src/index.html`

- The document declares HTML5 and English, sets UTF-8, theme color, page description, title, and the application base URL.
- The viewport meta tag enables the responsive layout on phones.
- The favicon link points to the generated public favicon.
- `<app-root>` is the host element where Angular mounts `App`.

### `client/src/app/app.spec.ts`

- The imports provide Angular’s test harness, the HTTP testing backend, the component, and its API service.
- `beforeEach` configures the standalone component with real `HttpClient` providers and the mock HTTP testing provider.
- `afterEach` verifies that no unexpected HTTP calls remain unhandled.
- The first test creates the component, triggers initial change detection, supplies mock scoreboard and game responses, and verifies nine board cells render.
- The second test calls `createGame('computer')`, checks the POST URL, method, and request body, then supplies a representative game response.
- These tests cover an Angular rendering path and the service’s request contract; they do not compile or test the .NET backend.

## 5. Interview questions to prepare for

**Why does the backend own the game rules?**
There is one authoritative state and validation path. A modified browser cannot bypass move validation, and other clients could use the same API without duplicating the rules.

**Why use a singleton store and a lock?**
The singleton shares state across requests in one API process. The lock prevents two simultaneous requests from interleaving state changes. This is suitable for a small local demo, not a scaled multi-instance deployment.

**What are the in-memory storage trade-offs?**
It is simple and requires no database setup, which meets the brief. It loses sessions and scores on restart and cannot share state across API instances. A database or distributed store would be the next step for persistence and scale.

**How does undo work in computer mode?**
The API treats the human move and automatic O reply as a pair. Undo removes both when the pair exists; if X wins before O can respond, it removes that single winning move. Undo also retracts a counted result from the scoreboard.

**How is the computer move selected?**
The API checks whether O can win, then whether X must be blocked, then chooses center, corner, or any available cell. The simulated cell is reset after each check.

**What does the lock not solve?**
It protects only this one process. Multiple API replicas would each have their own memory and lock. A shared database/distributed lock or atomic persistence strategy would be required for a multi-instance system.

**What would you improve next?**
Add backend unit/integration tests, configurable API URL by environment, persistent storage if needed, structured logging, and deployment-specific CORS. For production, also review concurrency and authorization/session ownership.

## 6. Run and verify

From the repository root, start the API and frontend in separate terminals:

```powershell
dotnet run --project server\TicTacToe.Api --urls http://localhost:5080
```

```powershell
cd client
npm install
npm start
```

Then browse to `http://localhost:4200`. Build and run the frontend tests with:

```powershell
cd client
npm run build
npx ng test --watch=false
```

The backend targets .NET 10. Install the .NET 10 SDK before running or building it.
