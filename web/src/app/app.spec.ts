import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the workspace title', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('LeviathanText');
  });

  it('renders the polytonic fixture texts without breaking combining sequences', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const greekCells = Array.from(compiled.querySelectorAll('.greek'));
    const texts = greekCells.map((cell) => cell.textContent ?? '');
    expect(texts.some((t) => t.includes('ἄνθρωπος'))).toBeTrue();
    expect(texts.some((t) => t.includes('ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ'))).toBeTrue();
    expect(texts.some((t) => t.includes('Κ\u0305\u03F2\u0305'))).toBeTrue();
  });
});
