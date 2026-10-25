export type Player = 'X' | 'O';
export type GameMode = 'twoPlayer' | 'computer';
export type GameStatus = 'inProgress' | 'won' | 'draw';

export interface GameMove {
  moveNumber: number;
  player: Player;
  row: number;
  column: number;
}

export interface GameState {
  id: string;
  mode: GameMode;
  board: (Player | null)[];
  currentPlayer: Player;
  moves: GameMove[];
  status: GameStatus;
  winner: Player | null;
  winningCells: number[];
}

export interface Scoreboard {
  xWins: number;
  oWins: number;
  draws: number;
}
