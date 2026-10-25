import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { finalize, Observable } from 'rxjs';
import { GameApiService } from './game-api.service';
import { GameMode, GameState, Player, Scoreboard } from './game.models';

@Component({
  selector: 'app-root',
  imports: [],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected readonly game = signal<GameState | null>(null);
  protected readonly scoreboard = signal<Scoreboard>({ xWins: 0, oWins: 0, draws: 0 });
  protected readonly mode = signal<GameMode>('twoPlayer');
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal('');

  constructor(private readonly api: GameApiService) {}

  ngOnInit(): void {
    this.refreshScoreboard();
    this.startGame();
  }

  protected startGame(): void {
    this.runRequest(this.api.createGame(this.mode()), game => this.game.set(game));
  }

  protected selectMode(mode: GameMode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    this.startGame();
  }

  protected play(cell: number): void {
    const game = this.game();
    if (!game || this.busy() || game.status !== 'inProgress' || game.board[cell] !== null) return;
    this.runRequest(this.api.playMove(game.id, cell), nextGame => {
      this.game.set(nextGame);
      this.refreshScoreboard();
    });
  }

  protected undo(): void {
    const game = this.game();
    if (!game || this.busy() || game.moves.length === 0) return;
    this.runRequest(this.api.undo(game.id), nextGame => {
      this.game.set(nextGame);
      this.refreshScoreboard();
    });
  }

  protected resetGame(): void {
    const game = this.game();
    if (!game || this.busy()) return;
    this.runRequest(this.api.resetGame(game.id), nextGame => this.game.set(nextGame));
  }

  protected resetScoreboard(): void {
    if (this.busy()) return;
    this.runRequest(this.api.resetScoreboard(), scores => this.scoreboard.set(scores));
  }

  protected cellLabel(cell: number): string {
    const row = Math.floor(cell / 3) + 1;
    const column = (cell % 3) + 1;
    const value = this.game()?.board[cell];
    return `Row ${row}, column ${column}${value ? `, ${value}` : ', empty'}`;
  }

  protected trackMove(_index: number, move: GameState['moves'][number]): number {
    return move.moveNumber;
  }

  protected statusText(game: GameState): string {
    if (game.status === 'won') return `${game.winner} takes the round`;
    if (game.status === 'draw') return 'A perfectly matched draw';
    return `${game.currentPlayer}’s turn`;
  }

  protected playerName(player: Player): string {
    return this.mode() === 'computer' && player === 'O' ? 'Computer' : `Player ${player}`;
  }

  private refreshScoreboard(): void {
    this.api.getScoreboard().subscribe({
      next: scores => this.scoreboard.set(scores),
      error: error => this.showError(error)
    });
  }

  private runRequest<T>(request: Observable<T>, onSuccess: (value: T) => void): void {
    this.busy.set(true);
    this.errorMessage.set('');
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: onSuccess,
      error: error => this.showError(error)
    });
  }

  private showError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      this.errorMessage.set(
        typeof error.error?.detail === 'string'
          ? error.error.detail
          : 'Could not reach the game server. Make sure the .NET API is running.'
      );
      return;
    }
    this.errorMessage.set('Something went wrong. Please try again.');
  }
}
