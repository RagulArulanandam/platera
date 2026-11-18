import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { AdminComment, AdminUser, Dashboard, Lookup, Paged, RecipeSummary } from '../core/models';
import { ToastService } from '../core/toast';
import { Pager } from '../shared/ui';

type Tab = 'overview' | 'users' | 'recipes' | 'comments' | 'taxonomy';

@Component({
  selector: 'app-admin',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, FormsModule, DatePipe, Pager],
  template: `
    <div class="container page">
      <h1>Admin</h1>
      <div class="tabs" role="tablist" aria-label="Admin sections">
        @for (t of tabs; track t.id) {
          <button role="tab" type="button" [attr.aria-selected]="tab() === t.id" [class.on]="tab() === t.id" (click)="select(t.id)">{{ t.label }}</button>
        }
      </div>

      @switch (tab()) {
        @case ('overview') {
          @if (dash(); as d) {
            <div class="stats">
              <div class="card card-pad stat"><span>{{ d.users }}</span>Users</div>
              <div class="card card-pad stat"><span>{{ d.recipes }}</span>Recipes</div>
              <div class="card card-pad stat"><span>{{ d.comments }}</span>Comments</div>
              <div class="card card-pad stat"><span>{{ d.likes }}</span>Likes</div>
              <div class="card card-pad stat"><span>{{ d.hiddenComments }}</span>Hidden comments</div>
            </div>
            <h2>Latest activity</h2>
            <ul class="activity card">
              @for (a of d.latestActivity; track $index) {
                <li><span class="chip" [class.chip-terra]="a.type === 'comment'">{{ a.type }}</span>
                  <span><strong>{{ a.actor }}</strong> — {{ a.description }}</span>
                  <span class="muted">{{ a.at | date: 'short' }}</span></li>
              }
            </ul>
          } @else { <p class="muted">Loading…</p> }
        }

        @case ('users') {
          <form class="bar" (submit)="loadUsers(1); $event.preventDefault()">
            <label class="sr-only" for="usearch">Search users</label>
            <input id="usearch" name="s" [(ngModel)]="userSearch" placeholder="Search by name or email" />
            <button class="btn" type="submit">Search</button>
          </form>
          <div class="card table-wrap">
            <table>
              <thead><tr><th>User</th><th>Role</th><th>Recipes</th><th>Joined</th><th>Status</th><th></th></tr></thead>
              <tbody>
                @for (u of users()?.items ?? []; track u.id) {
                  <tr>
                    <td><strong>{{ u.displayName }}</strong><br /><span class="muted">{{ u.email }}</span></td>
                    <td>
                      <select [attr.aria-label]="'Role for ' + u.displayName" [ngModel]="u.roles[0]" (ngModelChange)="setRole(u, $event)" [disabled]="isMe(u)">
                        <option value="User">User</option><option value="Admin">Admin</option>
                      </select>
                    </td>
                    <td>{{ u.recipeCount }}</td>
                    <td>{{ u.createdAt | date: 'mediumDate' }}</td>
                    <td><span class="chip" [class.chip-terra]="!u.isActive">{{ u.isActive ? 'Active' : 'Deactivated' }}</span></td>
                    <td><button class="btn btn-ghost btn-sm" type="button" (click)="toggleActive(u)" [disabled]="isMe(u)">{{ u.isActive ? 'Deactivate' : 'Activate' }}</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (users(); as p) { <app-pager [page]="p.page" [totalPages]="p.totalPages" (go)="loadUsers($event)" /> }
        }

        @case ('recipes') {
          <div class="card table-wrap">
            <table>
              <thead><tr><th>Recipe</th><th>Author</th><th>Likes</th><th>Featured</th><th></th></tr></thead>
              <tbody>
                @for (r of recipes()?.items ?? []; track r.id) {
                  <tr>
                    <td><a [routerLink]="['/recipes', r.id]">{{ r.title }}</a><br /><span class="muted">{{ r.cuisine }} · {{ r.category }}</span></td>
                    <td>{{ r.authorName }}</td>
                    <td>{{ r.likeCount }}</td>
                    <td><button class="btn btn-sm" [class.btn-ghost]="!r.isFeatured" type="button" (click)="toggleFeatured(r)">{{ r.isFeatured ? '★ Featured' : '☆ Feature' }}</button></td>
                    <td class="acts">
                      <a class="btn btn-ghost btn-sm" [routerLink]="['/recipes', r.id, 'edit']">Edit</a>
                      <button class="btn btn-danger btn-sm" type="button" (click)="deleteRecipe(r)">Delete</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (recipes(); as p) { <app-pager [page]="p.page" [totalPages]="p.totalPages" (go)="loadRecipes($event)" /> }
        }

        @case ('comments') {
          <label class="check"><input type="checkbox" [ngModel]="hiddenOnly()" (ngModelChange)="hiddenOnly.set($event); loadComments(1)" /> Show hidden only</label>
          <div class="card table-wrap">
            <table>
              <thead><tr><th>Comment</th><th>Recipe</th><th>Status</th><th></th></tr></thead>
              <tbody>
                @for (c of comments()?.items ?? []; track c.id) {
                  <tr>
                    <td><strong>{{ c.userName }}</strong> <span class="muted">{{ c.createdAt | date: 'short' }}</span><br />{{ c.text }}</td>
                    <td><a [routerLink]="['/recipes', c.recipeId]">{{ c.recipeTitle }}</a></td>
                    <td><span class="chip" [class.chip-terra]="c.isHidden">{{ c.isHidden ? 'Hidden' : 'Visible' }}</span></td>
                    <td class="acts">
                      <button class="btn btn-ghost btn-sm" type="button" (click)="toggleHidden(c)">{{ c.isHidden ? 'Unhide' : 'Hide' }}</button>
                      <button class="btn btn-danger btn-sm" type="button" (click)="deleteComment(c)">Delete</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (comments(); as p) { <app-pager [page]="p.page" [totalPages]="p.totalPages" (go)="loadComments($event)" /> }
        }

        @case ('taxonomy') {
          <div class="two">
            @for (kind of kinds; track kind.key) {
              <section class="card card-pad">
                <h2>{{ kind.label }}</h2>
                <ul class="lk">
                  @for (l of (kind.key === 'categories' ? categories() : cuisines()); track l.id) {
                    <li>
                      <input [value]="l.name" [attr.aria-label]="'Rename ' + l.name" #inp (keydown.enter)="renameLookup(kind.key, l, inp.value)" />
                      <button class="btn btn-ghost btn-sm" type="button" (click)="renameLookup(kind.key, l, inp.value)">Save</button>
                      <button class="btn btn-danger btn-sm" type="button" (click)="removeLookup(kind.key, l)">✕</button>
                    </li>
                  }
                </ul>
                <form class="add" (submit)="addLookup(kind.key, n.value); n.value = ''; $event.preventDefault()">
                  <input #n [attr.aria-label]="'New ' + kind.label" placeholder="Add new…" maxlength="60" />
                  <button class="btn btn-sm" type="submit">Add</button>
                </form>
              </section>
            }
          </div>
        }
      }
    </div>
  `,
  styles: `
    .tabs { display: flex; gap: 0.25rem; border-bottom: 1px solid var(--border); margin-bottom: 1.5rem; overflow-x: auto; }
    .tabs button { background: none; border: 0; border-bottom: 3px solid transparent; padding: 0.7rem 1rem; font: inherit; font-weight: 600; color: var(--muted); cursor: pointer; white-space: nowrap; }
    .tabs button.on { color: var(--primary); border-color: var(--primary); }
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
    .stat { text-align: center; color: var(--muted); font-weight: 600; }
    .stat span { display: block; font-family: var(--font-display); font-size: 2.4rem; color: var(--primary); }
    .activity { list-style: none; margin: 0; padding: 0; }
    .activity li { display: grid; grid-template-columns: 90px 1fr auto; gap: 1rem; padding: 0.8rem 1.2rem; border-bottom: 1px solid var(--border); align-items: center; }
    .activity li:last-child { border-bottom: 0; }
    .bar { display: flex; gap: 0.5rem; margin-bottom: 1rem; max-width: 460px; }
    .acts { white-space: nowrap; display: flex; gap: 0.4rem; }
    .check { display: inline-flex; gap: 0.5rem; align-items: center; margin-bottom: 1rem; }
    .check input { width: auto; }
    .two { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.5rem; }
    .lk { list-style: none; padding: 0; margin: 0 0 1rem; display: grid; gap: 0.5rem; }
    .lk li, .add { display: flex; gap: 0.4rem; }
    td select { min-width: 100px; }
    @media (max-width: 640px) { .activity li { grid-template-columns: 1fr; gap: 0.3rem; } }
  `,
})
export class AdminPage {
  private readonly api = inject(Api);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'overview', label: 'Overview' }, { id: 'users', label: 'Users' }, { id: 'recipes', label: 'Recipes' },
    { id: 'comments', label: 'Comments' }, { id: 'taxonomy', label: 'Categories & cuisines' },
  ];
  protected readonly kinds = [
    { key: 'categories' as const, label: 'Categories' },
    { key: 'cuisines' as const, label: 'Cuisines' },
  ];
  protected readonly tab = signal<Tab>('overview');
  protected readonly dash = signal<Dashboard | null>(null);
  protected readonly users = signal<Paged<AdminUser> | null>(null);
  protected readonly recipes = signal<Paged<RecipeSummary> | null>(null);
  protected readonly comments = signal<Paged<AdminComment> | null>(null);
  protected readonly categories = signal<Lookup[]>([]);
  protected readonly cuisines = signal<Lookup[]>([]);
  protected readonly hiddenOnly = signal(false);
  protected userSearch = '';

  constructor() { this.select('overview'); }

  protected isMe(u: AdminUser) { return u.id === this.auth.user()?.id; }

  protected select(t: Tab) {
    this.tab.set(t);
    if (t === 'overview') this.api.dashboard().subscribe((d) => this.dash.set(d));
    if (t === 'users') this.loadUsers(1);
    if (t === 'recipes') this.loadRecipes(1);
    if (t === 'comments') this.loadComments(1);
    if (t === 'taxonomy') this.loadLookups();
  }

  protected loadUsers(page: number) { this.api.adminUsers(this.userSearch.trim(), page).subscribe((p) => this.users.set(p)); }
  protected loadRecipes(page: number) { this.api.recipes({ page, pageSize: 15 }).subscribe((p) => this.recipes.set(p)); }
  protected loadComments(page: number) { this.api.adminComments(this.hiddenOnly(), page).subscribe((p) => this.comments.set(p)); }
  private loadLookups() {
    this.api.categories().subscribe((l) => this.categories.set(l));
    this.api.cuisines().subscribe((l) => this.cuisines.set(l));
  }

  private fail = (e: unknown) => this.toast.show(errorMessage(e), 'error');

  protected toggleActive(u: AdminUser) {
    this.api.setUserActive(u.id, !u.isActive).subscribe({
      next: () => { this.toast.show(`${u.displayName} ${u.isActive ? 'deactivated' : 'activated'}.`, 'success'); this.loadUsers(this.users()?.page ?? 1); },
      error: this.fail,
    });
  }
  protected setRole(u: AdminUser, role: string) {
    if (role === u.roles[0]) return;
    this.api.setUserRole(u.id, role).subscribe({
      next: () => { this.toast.show(`${u.displayName} is now ${role}.`, 'success'); this.loadUsers(this.users()?.page ?? 1); },
      error: (e) => { this.fail(e); this.loadUsers(this.users()?.page ?? 1); },
    });
  }
  protected toggleFeatured(r: RecipeSummary) {
    this.api.setFeatured(r.id, !r.isFeatured).subscribe({
      next: () => this.loadRecipes(this.recipes()?.page ?? 1),
      error: this.fail,
    });
  }
  protected deleteRecipe(r: RecipeSummary) {
    if (!confirm(`Delete “${r.title}”? This cannot be undone.`)) return;
    this.api.deleteRecipe(r.id).subscribe({ next: () => this.loadRecipes(this.recipes()?.page ?? 1), error: this.fail });
  }
  protected toggleHidden(c: AdminComment) {
    this.api.setCommentHidden(c.id, !c.isHidden).subscribe({ next: () => this.loadComments(this.comments()?.page ?? 1), error: this.fail });
  }
  protected deleteComment(c: AdminComment) {
    if (!confirm('Delete this comment permanently?')) return;
    this.api.adminDeleteComment(c.id).subscribe({ next: () => this.loadComments(this.comments()?.page ?? 1), error: this.fail });
  }
  protected addLookup(kind: 'categories' | 'cuisines', name: string) {
    if (!name.trim()) return;
    this.api.saveLookup(kind, name.trim()).subscribe({ next: () => this.loadLookups(), error: this.fail });
  }
  protected renameLookup(kind: 'categories' | 'cuisines', l: Lookup, name: string) {
    if (!name.trim() || name.trim() === l.name) return;
    this.api.saveLookup(kind, name.trim(), l.id).subscribe({
      next: () => { this.toast.show('Saved.', 'success'); this.loadLookups(); },
      error: this.fail,
    });
  }
  protected removeLookup(kind: 'categories' | 'cuisines', l: Lookup) {
    if (!confirm(`Delete “${l.name}”?`)) return;
    this.api.deleteLookup(kind, l.id).subscribe({ next: () => this.loadLookups(), error: this.fail });
  }
}
