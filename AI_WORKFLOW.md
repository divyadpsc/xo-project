# AI-Assisted Development Workflow and Engineering Decisions

## 1. Purpose of AI Assistance

AI assistance was used as a development aid while building the Tic-Tac-Toe application. It helped scaffold the Angular application, draft the user interface and REST API wiring, implement the in-memory game/session rules, and prepare the interview guide and project documentation.

AI-generated suggestions and code were reviewed against the assignment requirements. The developer remains responsible for understanding the implementation, validating its behavior, and explaining its design decisions.

## 2. Review and Validation

The implementation was reviewed against the problem statement and checked using the following validation steps:

- **Angular production build** — checked that the frontend compiles for production.
- **Frontend tests** — ran the available frontend test suite.
- **.NET build** — checked that the backend compiles.
- **Live API smoke test** — exercised the main workflow: create a game, request a computer move, undo a move, and retrieve the scoreboard.

These checks provide evidence for the builds and workflows tested. They do not guarantee complete test coverage or prove that every edge case has been handled. Only report a check as completed if it was actually run successfully against the submitted version.

## 3. Core Engineering Decisions

### Backend as the Source of Truth

The .NET API owns the authoritative game rules and scoreboard updates. Angular sends requests and renders the state returned by the API rather than independently deciding game outcomes.

This keeps business rules centralized and reduces the risk of the frontend and backend disagreeing about the game state.

### Singleton In-Memory Store

A singleton in-memory store holds game sessions and scoreboard data. This meets the assignment's requirements without requiring database installation or configuration.

The trade-off is that data is temporary: it is lost when the backend process restarts and is not automatically shared between multiple backend instances.

### Explicit TypeScript Types

Supported players, game modes, and game statuses are represented using explicit TypeScript types. This documents the expected values and helps catch certain mistakes during development.

TypeScript types are compile-time checks; runtime validation is still needed for untrusted API input. The backend validates game mode and move cell range, occupancy, and game status.

### Requirement-Driven Computer Strategy

The computer opponent uses a simple strategy that follows the priority defined in the problem statement. The implementation avoids unnecessary algorithmic complexity and keeps the behavior easier to understand and test.

The implemented priority is: take O's winning move, block X's immediate winning move, take the center, take an available corner, then take any available cell. It is a tactical strategy, not a minimax search or a guarantee of optimal play.

## 4. API and State Responsibilities

The frontend communicates with the backend through REST endpoints. The backend validates actions, applies game rules, updates the in-memory state, and returns results for the frontend to display.

For endpoint names, HTTP methods, request/response schemas, and error behavior, refer to the API endpoint summary in `README.md`. The API uses ASP.NET Core minimal API route mappings in `server/TicTacToe.Api/Program.cs`, with game rules and state operations in `server/TicTacToe.Api/GameStore.cs`.

## 5. Assumptions and Known Limitations

- Game sessions and scoreboard data are stored in memory; they are not persistent.
- Restarting the backend clears the in-memory state.
- Multiple backend instances would not automatically share game sessions or scores.
- The computer strategy is intentionally simple and is not intended to provide optimal play.
- Frontend and backend builds plus a smoke test do not replace comprehensive unit, integration, and edge-case testing.
- TypeScript types do not replace backend runtime validation.
- Production concerns such as persistence, multi-instance coordination, and stronger concurrency guarantees would require additional design and implementation.
- CORS is configured for the local Angular origin `http://localhost:4200`; a different frontend host requires an updated policy.
- The API has no authentication or authorization and is intended for a local demonstration.
- There is no automated .NET test project yet. Backend logic was built and smoke-tested, but should receive dedicated automated coverage.

Update this section if the code contains different behavior or if additional limitations are discovered during review.

## 6. Further Testing to Consider

Before treating the application as production-ready, consider testing:

- Invalid cell positions and attempts to play in an occupied cell.
- Moves made after a win or draw.
- Winner and draw detection for all relevant board patterns.
- Undo behavior in each supported game mode, including computer replies.
- Scoreboard consistency after undo, restart, and repeated games.
- Invalid request payloads and API error responses.
- Concurrent requests that could attempt to change the same game state.

## 7. Interview Discussion Points

Be ready to explain these decisions with concrete examples from the submitted code:

1. How an Angular action travels through the frontend service to a .NET endpoint and back.
2. Where the game rules are enforced and how the winner or draw is determined.
3. How the singleton in-memory store works and why it is appropriate for this assignment.
4. How undo restores the previous state and how it interacts with computer moves.
5. How the computer selects its move according to the required priority.
6. What the tests and smoke test validate, and what they do not validate.
7. How the design could evolve to use persistent storage and support multiple backend instances.

## 8. Transparency Statement

AI was used to accelerate scaffolding, implementation, and documentation. The resulting code was reviewed against the assignment and subjected to the validation steps described above. The final submission should accurately reflect the checks actually completed, and the developer should be able to explain and maintain the submitted implementation.
