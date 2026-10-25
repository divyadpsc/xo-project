import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { App } from './app';
import { GameApiService } from './game-api.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('renders all nine cells after the server creates a game', () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('http://localhost:5080/api/scoreboard').flush({ xWins: 0, oWins: 0, draws: 0 });
    http.expectOne('http://localhost:5080/api/games').flush({
      id: 'test-game',
      mode: 'twoPlayer',
      board: Array(9).fill(null),
      currentPlayer: 'X',
      moves: [],
      status: 'inProgress',
      winner: null,
      winningCells: [],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('.cell').length).toBe(9);
  });

  it('creates a computer game through the REST API', () => {
    const api = TestBed.inject(GameApiService);
    const http = TestBed.inject(HttpTestingController);
    api.createGame('computer').subscribe();

    const request = http.expectOne('http://localhost:5080/api/games');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ mode: 'computer' });
    request.flush({
      id: 'test-game',
      mode: 'computer',
      board: Array(9).fill(null),
      currentPlayer: 'X',
      moves: [],
      status: 'inProgress',
      winner: null,
      winningCells: [],
    });
  });
});
