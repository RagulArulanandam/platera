import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { Comment, Paged, RecipeDetail } from '../core/models';
import { ToastService } from '../core/toast';
import { Stars } from '../shared/ui';

@Component({
  selector: 'app-recipe-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Stars, FormsModule, DatePipe],
  template: `
    @if (notFound()) {
      <div class="narrow page" style="text-align:center"><h1>Recipe not found</h1><a class="btn" routerLink="/recipes">Browse recipes</a></div>
    } @else if (recipe(); as r) {
      <article class="container page">
        <nav class="crumbs muted" aria-label="Breadcrumb"><a routerLink="/recipes">Recipes</a> / {{ r.title }}</nav>

        <div class="top">
          <div class="gallery">
            @if (r.images.length) {
              <img class="main" [src]="r.images[active()].url" [alt]="r.images[active()].altText || r.title" />
              @if (r.images.length > 1) {
                <div class="thumbs">
                  @for (img of r.images; track img.url; let i = $index) {
                    <button type="button" [class.sel]="i === active()" (click)="active.set(i)" [attr.aria-label]="'Show photo ' + (i + 1)">
                      <img [src]="img.url" alt="" />
                    </button>
                  }
                </div>
              }
            }
          </div>

          <div class="info">
            <div class="tags"><span class="chip">{{ r.cuisine }}</span><span class="chip chip-terra">{{ r.category }}</span>
              @if (r.isFeatured) { <span class="chip">★ Featured</span> }</div>
            <h1>{{ r.title }}</h1>
            <p class="lead muted">{{ r.description }}</p>
            <p class="muted by">By <strong>{{ r.authorName }}</strong> · {{ r.createdAt | date: 'mediumDate' }}</p>

            <dl class="facts">
              <div><dt>Prep</dt><dd>{{ r.prepMinutes }} min</dd></div>
              <div><dt>Cook</dt><dd>{{ r.cookMinutes }} min</dd></div>
              <div><dt>Serves</dt><dd>{{ r.servings }}</dd></div>
              <div><dt>Level</dt><dd>{{ r.difficulty }}</dd></div>
            </dl>

            <div class="engage">
              <button class="btn" [class.btn-ghost]="!r.likedByMe" type="button" (click)="like()" [attr.aria-pressed]="r.likedByMe">
                {{ r.likedByMe ? '♥ Liked' : '♡ Like' }} · {{ r.likeCount }}
              </button>
              <div class="rate">
                <app-stars [value]="r.averageRating" [count]="r.ratingCount" />
              </div>
              @if (r.canEdit) {
                <a class="btn btn-ghost btn-sm" [routerLink]="['/recipes', r.id, 'edit']">Edit</a>
                <button class="btn btn-danger btn-sm" type="button" (click)="remove()">Delete</button>
              }
            </div>

            <div class="yours card card-pad">
              <span class="label">{{ auth.isAuthenticated() ? 'Your rating' : 'Log in to rate this recipe' }}</span>
              @if (auth.isAuthenticated()) { <app-stars [readonly]="false" [value]="r.myRating ?? 0" (rated)="rate($event)" /> }
              @else { <a routerLink="/login" [queryParams]="{ returnUrl: '/recipes/' + r.id }">Log in</a> }
            </div>
          </div>
        </div>

        <div class="cols">
          <section class="card card-pad" aria-labelledby="ing-h">
            <h2 id="ing-h">Ingredients</h2>
            <ul class="ing">@for (i of r.ingredients; track $index) { <li><strong>{{ i.quantity }}</strong> {{ i.name }}</li> }</ul>
          </section>
          <section class="card card-pad" aria-labelledby="steps-h">
            <h2 id="steps-h">Method</h2>
            <ol class="steps">@for (s of r.steps; track $index) { <li>{{ s }}</li> }</ol>
          </section>
        </div>

        @if (r.tags.length) {
          <div class="tagrow">@for (t of r.tags; track t) { <a class="chip" routerLink="/recipes" [queryParams]="{ tag: t }">#{{ t }}</a> }</div>
        }

        <section class="comments" aria-labelledby="c-h">
          <h2 id="c-h">I tried this ({{ comments()?.totalCount ?? r.commentCount }})</h2>

          @if (auth.isAuthenticated()) {
            <form class="card card-pad cform" (submit)="postComment($event)">
              <label class="label" for="ctext">Share how it went</label>
              <textarea id="ctext" name="text" [(ngModel)]="text" maxlength="2000" placeholder="What did you change? How did it turn out?"></textarea>
              <div class="crow">
                <label class="btn btn-ghost btn-sm file">
                  📷 {{ photoName() || 'Add a photo' }}
                  <input class="sr-only" type="file" accept="image/png,image/jpeg,image/webp,image/gif" (change)="pickPhoto($event)" />
                </label>
                <button class="btn" type="submit" [disabled]="posting() || !text.trim()">{{ posting() ? 'Posting…' : 'Post' }}</button>
              </div>
            </form>
          } @else {
            <p class="muted"><a routerLink="/login" [queryParams]="{ returnUrl: '/recipes/' + r.id }">Log in</a> to share your experience.</p>
          }

          <ul class="clist">
            @for (c of comments()?.items ?? []; track c.id) {
              <li class="card card-pad" [class.hidden]="c.isHidden">
                <div class="chead"><strong>{{ c.userName }}</strong><span class="muted">{{ c.createdAt | date: 'medium' }}</span></div>
                @if (c.isHidden) { <span class="chip chip-terra">Hidden by moderator</span> }
                <p>{{ c.text }}</p>
                @if (c.photoUrl) { <img class="cphoto" [src]="c.photoUrl" alt="Photo shared with this comment" loading="lazy" /> }
                @if (c.canDelete) { <button class="btn btn-ghost btn-sm" type="button" (click)="deleteComment(c)">Delete</button> }
              </li>
            }
          </ul>
          @if (comments() && comments()!.page < comments()!.totalPages) {
            <button class="btn btn-ghost" type="button" (click)="loadMore()">Load more comments</button>
          }
        </section>
      </article>
    } @else {
      <div class="container page"><p class="muted">Loading recipe…</p></div>
    }
  `,
  styles: `
    .crumbs { font-size: 0.9rem; margin-bottom: 1rem; }
    .top { display: grid; grid-template-columns: 1.05fr 1fr; gap: 2rem; align-items: start; }
    .gallery .main { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: var(--radius); box-shadow: var(--shadow); background: var(--bg-alt); }
    .thumbs { display: flex; gap: 0.5rem; margin-top: 0.6rem; flex-wrap: wrap; }
    .thumbs button { padding: 0; border: 2px solid transparent; border-radius: 10px; overflow: hidden; width: 72px; height: 54px; cursor: pointer; background: none; }
    .thumbs button.sel { border-color: var(--primary); }
    .thumbs img { width: 100%; height: 100%; object-fit: cover; }
    .tags { display: flex; gap: 0.4rem; flex-wrap: wrap; margin-bottom: 0.6rem; }
    .lead { font-size: 1.1rem; }
    .by { margin-bottom: 1rem; }
    .facts { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0.5rem; margin: 0 0 1.25rem; padding: 0.9rem; background: var(--bg-alt); border-radius: var(--radius-sm); }
    .facts div { text-align: center; }
    .facts dt { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--muted); }
    .facts dd { margin: 0; font-weight: 700; }
    .engage { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; margin-bottom: 1rem; }
    .yours { display: flex; gap: 1rem; align-items: center; justify-content: space-between; flex-wrap: wrap; }
    .cols { display: grid; grid-template-columns: 0.8fr 1.2fr; gap: 1.5rem; margin-top: 2rem; align-items: start; }
    .ing { list-style: none; margin: 0; padding: 0; }
    .ing li { padding: 0.5rem 0; border-bottom: 1px dashed var(--border); }
    .steps { margin: 0; padding-left: 1.4rem; }
    .steps li { margin-bottom: 1rem; padding-left: 0.3rem; }
    .steps li::marker { color: var(--primary); font-weight: 700; font-family: var(--font-display); }
    .tagrow { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-top: 1.5rem; }
    .comments { margin-top: 3rem; max-width: 760px; }
    .cform textarea { margin: 0.4rem 0 0.8rem; }
    .crow { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
    .clist { list-style: none; padding: 0; margin: 1.25rem 0; display: grid; gap: 1rem; }
    .chead { display: flex; justify-content: space-between; gap: 1rem; margin-bottom: 0.4rem; }
    .clist li.hidden { opacity: 0.6; }
    .cphoto { max-width: 280px; border-radius: var(--radius-sm); margin: 0.4rem 0 0.8rem; }
    .file { cursor: pointer; }
    @media (max-width: 820px) { .top, .cols { grid-template-columns: 1fr; } }
  `,
})
export class RecipeDetailPage {
  readonly id = input.required<string>();

  protected readonly auth = inject(AuthService);
  private readonly api = inject(Api);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly recipe = signal<RecipeDetail | null>(null);
  protected readonly notFound = signal(false);
  protected readonly active = signal(0);
  protected readonly comments = signal<Paged<Comment> | null>(null);
  protected readonly posting = signal(false);
  protected readonly photoName = signal('');
  protected text = '';
  private photo: File | null = null;

  constructor() {
    effect(() => {
      const id = this.id();
      this.recipe.set(null);
      this.notFound.set(false);
      this.active.set(0);
      this.api.recipe(id).subscribe({
        next: (r) => this.recipe.set(r),
        error: (e) => (e?.status === 404 ? this.notFound.set(true) : this.toast.show(errorMessage(e), 'error')),
      });
      this.api.comments(id).subscribe({ next: (c) => this.comments.set(c), error: () => undefined });
    });
  }

  protected like() {
    if (!this.requireLogin()) return;
    const r = this.recipe()!;
    this.api.toggleLike(r.id).subscribe({
      next: (s) => this.recipe.set({ ...r, likedByMe: s.liked, likeCount: s.likeCount }),
      error: (e) => this.toast.show(errorMessage(e), 'error'),
    });
  }

  protected rate(stars: number) {
    const r = this.recipe()!;
    this.api.rate(r.id, stars).subscribe({
      next: (s) => this.recipe.set({ ...r, myRating: s.myRating, averageRating: s.averageRating, ratingCount: s.ratingCount }),
      error: (e) => this.toast.show(errorMessage(e), 'error'),
    });
  }

  protected pickPhoto(e: Event) {
    const f = (e.target as HTMLInputElement).files?.[0] ?? null;
    if (f && f.size > 5 * 1024 * 1024) {
      this.toast.show('Photos must be 5 MB or smaller.', 'error');
      return;
    }
    this.photo = f;
    this.photoName.set(f?.name ?? '');
  }

  protected postComment(e: Event) {
    e.preventDefault();
    const r = this.recipe()!;
    const text = this.text.trim();
    if (!text) return;
    this.posting.set(true);
    const send = (url: string | null) =>
      this.api.addComment(r.id, text, url).subscribe({
        next: (c) => {
          const cur = this.comments();
          this.comments.set(cur ? { ...cur, items: [c, ...cur.items], totalCount: cur.totalCount + 1 } : null);
          this.text = '';
          this.photo = null;
          this.photoName.set('');
          this.posting.set(false);
          this.toast.show('Thanks for sharing!', 'success');
        },
        error: (err) => { this.toast.show(errorMessage(err), 'error'); this.posting.set(false); },
      });
    if (this.photo) {
      this.api.uploadImage(this.photo).subscribe({
        next: (u) => send(u.url),
        error: (err) => { this.toast.show(errorMessage(err), 'error'); this.posting.set(false); },
      });
    } else send(null);
  }

  protected deleteComment(c: Comment) {
    this.api.deleteComment(c.id).subscribe({
      next: () => {
        const cur = this.comments();
        if (cur) this.comments.set({ ...cur, items: cur.items.filter((x) => x.id !== c.id), totalCount: cur.totalCount - 1 });
      },
      error: (e) => this.toast.show(errorMessage(e), 'error'),
    });
  }

  protected loadMore() {
    const cur = this.comments();
    if (!cur) return;
    this.api.comments(this.id(), cur.page + 1).subscribe((next) =>
      this.comments.set({ ...next, items: [...cur.items, ...next.items] }));
  }

  protected remove() {
    const r = this.recipe()!;
    if (!confirm(`Delete “${r.title}”? This cannot be undone.`)) return;
    this.api.deleteRecipe(r.id).subscribe({
      next: () => { this.toast.show('Recipe deleted.', 'success'); void this.router.navigateByUrl('/profile'); },
      error: (e) => this.toast.show(errorMessage(e), 'error'),
    });
  }

  private requireLogin(): boolean {
    if (this.auth.isAuthenticated()) return true;
    void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    return false;
  }
}
