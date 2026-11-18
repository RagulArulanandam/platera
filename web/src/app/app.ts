import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth';
import { ThemeService } from './core/theme';
import { ToastService } from './core/toast';
import { Logo } from './shared/ui';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Logo],
  template: `
    <a class="skip" href="#main">Skip to content</a>
    <header class="top">
      <div class="container bar">
        <a routerLink="/" class="brand" aria-label="Platera home"><app-logo /></a>

        <button class="burger btn-ghost btn" type="button" (click)="menu.set(!menu())"
                [attr.aria-expanded]="menu()" aria-controls="nav" aria-label="Toggle menu">☰</button>

        <nav id="nav" [class.open]="menu()" aria-label="Main" (click)="menu.set(false)">
          <a routerLink="/recipes" routerLinkActive="active">Browse</a>
          @if (auth.isAuthenticated()) {
            <a routerLink="/recipes/new" routerLinkActive="active">Share a recipe</a>
            <a routerLink="/profile" routerLinkActive="active">My kitchen</a>
            @if (auth.isAdmin()) { <a routerLink="/admin" routerLinkActive="active">Admin</a> }
          }
        </nav>

        <form class="search" role="search" (submit)="search($event, q.value)">
          <label class="sr-only" for="global-search">Search recipes</label>
          <input #q id="global-search" type="search" placeholder="Search recipes…" />
        </form>

        <div class="actions">
          <button class="btn btn-ghost btn-sm" type="button" (click)="theme.toggle()"
                  [attr.aria-label]="theme.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'">
            {{ theme.theme() === 'dark' ? '☀️' : '🌙' }}
          </button>
          @if (auth.user(); as u) {
            <span class="hello muted">Hi, {{ u.displayName }}</span>
            <button class="btn btn-ghost btn-sm" type="button" (click)="auth.logout()">Log out</button>
          } @else {
            <a class="btn btn-ghost btn-sm" routerLink="/login">Log in</a>
            <a class="btn btn-sm" routerLink="/register">Join</a>
          }
        </div>
      </div>
    </header>

    <main id="main" tabindex="-1"><router-outlet /></main>

    <footer class="foot">
      <div class="container">
        <app-logo [size]="28" />
        <p class="muted">Every dish has a story. Share yours.</p>
      </div>
    </footer>

    <div class="toasts" aria-live="polite">
      @for (t of toast.toasts(); track t.id) {
        <div class="toast" [class.err]="t.kind === 'error'" [class.ok]="t.kind === 'success'" role="status">
          {{ t.message }}
          <button type="button" (click)="toast.dismiss(t.id)" aria-label="Dismiss">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100vh; }
    main { flex: 1; outline: none; }
    .skip { position: absolute; left: -999px; top: 0; background: var(--primary); color: var(--on-primary); padding: 0.5rem 1rem; z-index: 100; }
    .skip:focus { left: 0.5rem; top: 0.5rem; }
    .top { position: sticky; top: 0; z-index: 20; background: color-mix(in srgb, var(--bg) 88%, transparent); backdrop-filter: blur(10px); border-bottom: 1px solid var(--border); }
    .bar { display: flex; align-items: center; gap: 1rem; padding: 0.7rem 0; flex-wrap: wrap; }
    .brand { text-decoration: none; }
    nav { display: flex; gap: 1.1rem; margin-left: 0.5rem; }
    nav a { color: var(--text); text-decoration: none; font-weight: 500; padding: 0.25rem 0.1rem; border-bottom: 2px solid transparent; }
    nav a:hover, nav a.active { border-color: var(--primary); color: var(--primary); }
    .search { flex: 1; min-width: 160px; max-width: 320px; margin-left: auto; }
    .search input { border-radius: 999px; padding: 0.5rem 1rem; }
    .actions { display: flex; align-items: center; gap: 0.5rem; }
    .hello { font-size: 0.9rem; }
    .burger { display: none; }
    .foot { border-top: 1px solid var(--border); padding: 2rem 0; margin-top: 2rem; background: var(--bg-alt); }
    .foot .container { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem; }
    .foot p { margin: 0; }
    .toasts { position: fixed; bottom: 1rem; right: 1rem; display: grid; gap: 0.5rem; z-index: 50; max-width: min(380px, 92vw); }
    .toast { background: var(--text); color: var(--bg); padding: 0.7rem 1rem; border-radius: var(--radius-sm); box-shadow: var(--shadow-hover); display: flex; gap: 0.75rem; justify-content: space-between; align-items: center; }
    .toast.err { background: var(--danger); color: #fff; }
    .toast.ok { background: var(--accent); color: #fff; }
    .toast button { background: none; border: 0; color: inherit; font-size: 1.2rem; cursor: pointer; line-height: 1; }
    @media (max-width: 860px) {
      .burger { display: inline-flex; margin-left: auto; padding: 0.3rem 0.7rem; }
      .search { order: 5; max-width: none; flex-basis: 100%; margin-left: 0; }
      nav { display: none; flex-basis: 100%; flex-direction: column; gap: 0.4rem; margin: 0; order: 4; }
      nav.open { display: flex; }
      .actions { order: 3; }
      .hello { display: none; }
    }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly menu = signal(false);

  protected search(e: Event, q: string) {
    e.preventDefault();
    void this.router.navigate(['/recipes'], { queryParams: { q: q.trim() || null } });
  }
}
