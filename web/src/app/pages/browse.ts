import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, of, switchMap, tap } from 'rxjs';
import { Api } from '../core/api';
import { Lookup, Paged, RecipeQuery, RecipeSort, RecipeSummary } from '../core/models';
import { Pager, RecipeCard } from '../shared/ui';

@Component({
  selector: 'app-browse',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RecipeCard, Pager],
  template: `
    <div class="container page">
      <h1>Browse recipes</h1>

      <form class="filters card card-pad" (submit)="apply($event)" aria-label="Search and filters">
        <div class="field wide">
          <label for="q">Keyword</label>
          <input id="q" name="q" type="search" #q [value]="query().q ?? ''" placeholder="Title, ingredient or tag" />
        </div>
        <div class="field">
          <label for="cuisine">Cuisine</label>
          <select id="cuisine" #cuisine [value]="query().cuisineId ?? ''">
            <option value="">Any</option>
            @for (c of cuisines(); track c.id) { <option [value]="c.id" [selected]="c.id === query().cuisineId">{{ c.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label for="category">Category</label>
          <select id="category" #category>
            <option value="">Any</option>
            @for (c of categories(); track c.id) { <option [value]="c.id" [selected]="c.id === query().categoryId">{{ c.name }}</option> }
          </select>
        </div>
        <div class="field">
          <label for="cook">Max cook time</label>
          <select id="cook" #cook>
            <option value="">Any</option>
            @for (m of cookTimes; track m) { <option [value]="m" [selected]="m === query().maxCookMinutes">≤ {{ m }} min</option> }
          </select>
        </div>
        <div class="field">
          <label for="rating">Min rating</label>
          <select id="rating" #rating>
            <option value="">Any</option>
            @for (s of [4, 3, 2]; track s) { <option [value]="s" [selected]="s === query().minRating">{{ s }}★ &amp; up</option> }
          </select>
        </div>
        <div class="field">
          <label for="sort">Sort by</label>
          <select id="sort" #sort>
            @for (o of sorts; track o.v) { <option [value]="o.v" [selected]="o.v === (query().sort ?? 'Newest')">{{ o.l }}</option> }
          </select>
        </div>
        <div class="actions">
          <button class="btn" type="submit">Apply</button>
          <button class="btn btn-ghost" type="button" (click)="reset()">Reset</button>
        </div>
      </form>

      <p class="muted count" aria-live="polite">
        @if (loading()) { Loading… } @else { {{ result()?.totalCount ?? 0 }} recipe{{ result()?.totalCount === 1 ? '' : 's' }} found }
        @if (query().tag) { · tag “{{ query().tag }}” }
      </p>

      <div class="grid">@for (r of result()?.items ?? []; track r.id) { <app-recipe-card [recipe]="r" /> }</div>

      @if (!loading() && (result()?.items?.length ?? 0) === 0) {
        <div class="empty card card-pad"><h3>Nothing cooking here yet</h3><p class="muted">Try fewer filters or a different keyword.</p></div>
      }

      @if (result(); as r) { <app-pager [page]="r.page" [totalPages]="r.totalPages" (go)="goTo($event)" /> }
    </div>
  `,
  styles: `
    .filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 0.25rem 1rem; align-items: end; margin: 1rem 0; }
    .filters .field { margin-bottom: 0.5rem; }
    .wide { grid-column: span 2; }
    .actions { display: flex; gap: 0.5rem; margin-bottom: 0.5rem; }
    .count { margin: 1rem 0; }
    .empty { text-align: center; margin-top: 1rem; }
    @media (max-width: 560px) { .wide { grid-column: span 1; } }
  `,
})
export class BrowsePage {
  private readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly cuisines = signal<Lookup[]>([]);
  protected readonly categories = signal<Lookup[]>([]);
  protected readonly result = signal<Paged<RecipeSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly query = signal<RecipeQuery>({});
  protected readonly cookTimes = [15, 30, 60, 120];
  protected readonly sorts: { v: RecipeSort; l: string }[] = [
    { v: 'Newest', l: 'Newest' }, { v: 'TopRated', l: 'Top rated' }, { v: 'MostLiked', l: 'Most liked' }, { v: 'Trending', l: 'Trending' },
  ];

  constructor() {
    this.api.cuisines().subscribe({ next: (r) => this.cuisines.set(r), error: () => undefined });
    this.api.categories().subscribe({ next: (r) => this.categories.set(r), error: () => undefined });

    this.route.queryParamMap
      .pipe(
        tap((p) => {
          this.loading.set(true);
          this.query.set({
            q: p.get('q') ?? undefined,
            cuisineId: num(p.get('cuisineId')),
            categoryId: num(p.get('categoryId')),
            maxCookMinutes: num(p.get('maxCookMinutes')),
            minRating: num(p.get('minRating')),
            tag: p.get('tag') ?? undefined,
            sort: (p.get('sort') as RecipeSort | null) ?? undefined,
            page: num(p.get('page')) ?? 1,
          });
        }),
        switchMap(() => this.api.recipes({ ...this.query(), pageSize: 12 }).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((r) => {
        this.result.set(r);
        this.loading.set(false);
      });
  }

  protected apply(e: Event) {
    e.preventDefault();
    const form = e.target as HTMLFormElement;
    const val = (id: string) => (form.querySelector('#' + id) as HTMLInputElement | HTMLSelectElement).value || null;
    void this.router.navigate([], {
      queryParams: {
        q: val('q')?.trim() || null, cuisineId: val('cuisine'), categoryId: val('category'),
        maxCookMinutes: val('cook'), minRating: val('rating'), sort: val('sort') === 'Newest' ? null : val('sort'),
        tag: this.query().tag ?? null, page: null,
      },
    });
  }

  protected reset() {
    void this.router.navigate([], { queryParams: {} });
  }

  protected goTo(page: number) {
    void this.router.navigate([], { queryParams: { page: page === 1 ? null : page }, queryParamsHandling: 'merge' });
  }
}

function num(v: string | null): number | undefined {
  if (v === null || v === '') return undefined;
  const n = Number(v);
  return Number.isFinite(n) ? n : undefined;
}
