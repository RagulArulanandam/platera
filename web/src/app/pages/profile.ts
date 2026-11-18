import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { AuthService } from '../core/auth';
import { Paged, RecipeSummary } from '../core/models';
import { Pager, RecipeCard } from '../shared/ui';

type Tab = 'mine' | 'liked';

@Component({
  selector: 'app-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RecipeCard, Pager],
  template: `
    <div class="container page">
      <header class="head">
        <div class="avatar" aria-hidden="true">{{ initial() }}</div>
        <div>
          <h1>{{ auth.user()?.displayName }}'s kitchen</h1>
          <p class="muted">{{ auth.user()?.email }}</p>
        </div>
        <a class="btn" routerLink="/recipes/new">＋ Share a recipe</a>
      </header>

      <div class="tabs" role="tablist" aria-label="Profile sections">
        <button role="tab" type="button" [attr.aria-selected]="tab() === 'mine'" [class.on]="tab() === 'mine'" (click)="select('mine')">My recipes</button>
        <button role="tab" type="button" [attr.aria-selected]="tab() === 'liked'" [class.on]="tab() === 'liked'" (click)="select('liked')">Liked recipes</button>
      </div>

      @if (loading()) { <p class="muted">Loading…</p> }
      <div class="grid">@for (r of data()?.items ?? []; track r.id) { <app-recipe-card [recipe]="r" /> }</div>

      @if (!loading() && (data()?.items?.length ?? 0) === 0) {
        <div class="card card-pad empty">
          @if (tab() === 'mine') { <h3>You haven't shared a recipe yet</h3><a class="btn" routerLink="/recipes/new">Share your first dish</a> }
          @else { <h3>No liked recipes yet</h3><a class="btn" routerLink="/recipes">Find something delicious</a> }
        </div>
      }
      @if (data(); as d) { <app-pager [page]="d.page" [totalPages]="d.totalPages" (go)="load($event)" /> }
    </div>
  `,
  styles: `
    .head { display: flex; gap: 1.25rem; align-items: center; flex-wrap: wrap; margin-bottom: 1.5rem; }
    .head h1 { font-size: 2rem; margin: 0; }
    .head p { margin: 0; }
    .head .btn { margin-left: auto; }
    .avatar { width: 64px; height: 64px; border-radius: 50%; background: var(--primary); color: var(--on-primary); display: grid; place-items: center; font-family: var(--font-display); font-size: 1.8rem; font-weight: 700; }
    .tabs { display: flex; gap: 0.5rem; border-bottom: 1px solid var(--border); margin-bottom: 1.5rem; }
    .tabs button { background: none; border: 0; border-bottom: 3px solid transparent; padding: 0.7rem 1rem; font: inherit; font-weight: 600; color: var(--muted); cursor: pointer; }
    .tabs button.on { color: var(--primary); border-color: var(--primary); }
    .empty { text-align: center; margin-top: 1rem; }
  `,
})
export class ProfilePage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(Api);
  protected readonly tab = signal<Tab>('mine');
  protected readonly data = signal<Paged<RecipeSummary> | null>(null);
  protected readonly loading = signal(true);

  constructor() { this.load(1); }

  protected initial() { return (this.auth.user()?.displayName ?? '?').charAt(0).toUpperCase(); }

  protected select(t: Tab) {
    this.tab.set(t);
    this.load(1);
  }

  protected load(page: number) {
    this.loading.set(true);
    const req = this.tab() === 'mine' ? this.api.mine(page) : this.api.liked(page);
    req.subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
