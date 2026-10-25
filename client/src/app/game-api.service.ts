import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { GameMode, GameState, Scoreboard } from './game.models';

@Injectable({ providedIn: 'root' })
export class GameApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5080/api';

  createGame(mode: GameMode): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games`, { mode });
  }

  playMove(gameId: string, cell: number): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/moves`, { cell });
  }

  undo(gameId: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/undo`, {});
  }

  resetGame(gameId: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/reset`, {});
  }

  getScoreboard(): Observable<Scoreboard> {
    return this.http.get<Scoreboard>(`${this.baseUrl}/scoreboard`);
  }

  resetScoreboard(): Observable<Scoreboard> {
    return this.http.post<Scoreboard>(`${this.baseUrl}/scoreboard/reset`, {});
  }
}
