import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { Lookup, RecipeSummary } from '../core/models';
import { RecipeCard } from '../shared/ui';

@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RecipeCard],
  template: `
    <section class="hero">
      <div class="container hero-inner">
        <div class="copy">
          <p class="eyebrow">A community recipe book</p>
          <h1>Every dish has a <em>story</em>.</h1>
          <p class="lead">Discover home-cooked favourites from kitchens around the world — and share the one your family keeps asking for.</p>
          <form class="hero-search" role="search" (submit)="search($event, q.value)">
            <label class="sr-only" for="hero-q">Search recipes, ingredients or tags</label>
            <input #q id="hero-q" type="search" placeholder="Try “pasta”, “vegan” or “chickpeas”…" />
            <button class="btn" type="submit">Search</button>
          </form>
          <div class="cta">
            <a class="btn btn-ghost" routerLink="/recipes">Browse all recipes</a>
          </div>
        </div>
        <div class="art" aria-hidden="true">
          <svg viewBox="0 0 400 400">
            <circle cx="200" cy="200" r="190" fill="var(--primary-soft)" />
            <circle cx="200" cy="200" r="150" fill="var(--surface)" stroke="var(--border)" stroke-width="4" />
            <circle cx="200" cy="200" r="105" fill="var(--primary)" opacity="0.92" />
            <path d="M200 120c40 22 44 70 0 120-44-50-40-98 0-120z" fill="var(--accent)" stroke="var(--surface)" stroke-width="4" />
            <path d="M200 135v95" stroke="var(--surface)" stroke-width="5" stroke-linecap="round" />
            <circle cx="95" cy="95" r="14" fill="var(--accent)" opacity="0.5" />
            <circle cx="320" cy="90" r="9" fill="var(--primary)" opacity="0.5" />
            <circle cx="330" cy="320" r="16" fill="var(--accent)" opacity="0.4" />
          </svg>
        </div>
      </div>
    </section>

    <div class="container page">
      <section aria-labelledby="featured-h">
        <div class="section-head"><h2 id="featured-h">Featured recipes</h2><a routerLink="/recipes" [queryParams]="{ sort: 'TopRated' }">See top rated →</a></div>
        @if (loading()) { <p class="muted">Loading…</p> }
        <div class="grid">@for (r of featured(); track r.id) { <app-recipe-card [recipe]="r" /> }</div>
      </section>

      <section aria-labelledby="trending-h" class="block">
        <div class="section-head"><h2 id="trending-h">Trending this week</h2><a routerLink="/recipes" [queryParams]="{ sort: 'Trending' }">See more →</a></div>
        <div class="grid">@for (r of trending(); track r.id) { <app-recipe-card [recipe]="r" /> }</div>
      </section>

      <section aria-labelledby="cuisines-h" class="block">
        <h2 id="cuisines-h">Explore by cuisine</h2>
        <div class="chips">
          @for (c of cuisines(); track c.id) {
            <a class="chip" routerLink="/recipes" [queryParams]="{ cuisineId: c.id }">{{ c.name }}</a>
          }
        </div>
      </section>
    </div>
  `,
  styles: `
    .hero { background: linear-gradient(180deg, var(--bg-alt), var(--bg)); border-bottom: 1px solid var(--border); }
    .hero-inner { display: grid; grid-template-columns: 1.2fr 0.8fr; gap: 2rem; align-items: center; padding: 3.5rem 0; }
    .eyebrow { color: var(--accent); font-weight: 700; text-transform: uppercase; letter-spacing: 0.12em; font-size: 0.8rem; margin-bottom: 0.5rem; }
    h1 em { color: var(--primary); font-style: italic; }
    .lead { font-size: 1.15rem; color: var(--muted); max-width: 52ch; }
    .hero-search { display: flex; gap: 0.5rem; max-width: 520px; margin: 1.5rem 0 1rem; }
    .hero-search input { border-radius: 999px; padding: 0.8rem 1.2rem; }
    .art svg { width: min(100%, 380px); margin-inline: auto; animation: float 7s ease-in-out infinite; }
    @keyframes float { 50% { transform: translateY(-10px) rotate(2deg); } }
    .block { margin-top: 3rem; }
    .chips { display: flex; flex-wrap: wrap; gap: 0.6rem; }
    .chips .chip { font-size: 0.95rem; padding: 0.4rem 1rem; transition: transform 0.15s ease; }
    .chips .chip:hover { transform: translateY(-2px); }
    @media (max-width: 760px) { .hero-inner { grid-template-columns: 1fr; padding: 2rem 0; } .art { display: none; } }
  `,
})
export class HomePage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly featured = signal<RecipeSummary[]>([]);
  protected readonly trending = signal<RecipeSummary[]>([]);
  protected readonly cuisines = signal<Lookup[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.api.featured(6).subscribe({ next: (r) => { this.featured.set(r); this.loading.set(false); }, error: () => this.loading.set(false) });
    this.api.trending(6).subscribe({ next: (r) => this.trending.set(r), error: () => undefined });
    this.api.cuisines().subscribe({ next: (r) => this.cuisines.set(r), error: () => undefined });
  }

  protected search(e: Event, q: string) {
    e.preventDefault();
    void this.router.navigate(['/recipes'], { queryParams: { q: q.trim() || null } });
  }
}
