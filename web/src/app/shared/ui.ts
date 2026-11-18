import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RecipeSummary } from '../core/models';

@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.height]="size()" viewBox="0 0 64 64" aria-hidden="true" focusable="false">
      <circle cx="32" cy="32" r="30" fill="var(--primary)" />
      <circle cx="32" cy="32" r="21" fill="none" stroke="var(--bg)" stroke-width="3" />
      <path d="M32 14c10 5 11 17 0 28-11-11-10-23 0-28z" fill="var(--accent)" stroke="var(--bg)" stroke-width="1.5" />
      <path d="M32 18v22" stroke="var(--bg)" stroke-width="2.5" stroke-linecap="round" />
    </svg>
    @if (wordmark()) {
      <span class="word">Platera</span>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 0.55rem; }
    .word { font-family: var(--font-display); font-weight: 700; font-size: 1.55rem; color: var(--text); letter-spacing: -0.02em; }
  `,
})
export class Logo {
  readonly size = input(36);
  readonly wordmark = input(true);
}

@Component({
  selector: 'app-stars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (readonly()) {
      <span class="stars" role="img" [attr.aria-label]="label()">
        @for (n of nums; track n) {
          <span class="s" [class.on]="n <= rounded()" aria-hidden="true">★</span>
        }
        @if (showValue()) {
          <span class="val">{{ value() > 0 ? value().toFixed(1) : '–' }}@if (count() !== null) { <span class="muted">({{ count() }})</span> }</span>
        }
      </span>
    } @else {
      <span class="stars input" role="radiogroup" aria-label="Your rating">
        @for (n of nums; track n) {
          <button type="button" class="s btn-star" role="radio" [class.on]="n <= (hover() || value())"
                  [attr.aria-checked]="n === value()" [attr.aria-label]="n + ' star' + (n > 1 ? 's' : '')"
                  (mouseenter)="hover.set(n)" (mouseleave)="hover.set(0)" (click)="rated.emit(n)">★</button>
        }
      </span>
    }
  `,
  styles: `
    .stars { display: inline-flex; align-items: center; gap: 0.1rem; }
    .s { color: var(--border); font-size: 1.05rem; line-height: 1; }
    .s.on { color: var(--star); }
    .btn-star { background: none; border: 0; padding: 0.1rem; cursor: pointer; font-size: 1.7rem; transition: transform 0.12s ease; }
    .btn-star:hover { transform: scale(1.15); }
    .val { margin-left: 0.4rem; font-size: 0.85rem; font-weight: 600; }
  `,
})
export class Stars {
  readonly value = input(0);
  readonly count = input<number | null>(null);
  readonly readonly = input(true);
  readonly showValue = input(true);
  readonly rated = output<number>();
  protected readonly nums = [1, 2, 3, 4, 5];
  protected readonly hover = signal(0);
  protected readonly rounded = computed(() => Math.round(this.value()));
  protected readonly label = computed(() => `Rated ${this.value().toFixed(1)} out of 5`);
}

@Component({
  selector: 'app-recipe-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Stars],
  template: `
    <article class="card rc">
      <a class="media" [routerLink]="['/recipes', recipe().id]" [attr.aria-label]="'View ' + recipe().title">
        @if (recipe().coverImageUrl; as src) {
          <img [src]="src" [alt]="'Photo of ' + recipe().title" loading="lazy" />
        }
        @if (recipe().isFeatured) { <span class="badge">Featured</span> }
      </a>
      <div class="body">
        <div class="tags"><span class="chip">{{ recipe().cuisine }}</span><span class="chip chip-terra">{{ recipe().category }}</span></div>
        <h3><a [routerLink]="['/recipes', recipe().id]">{{ recipe().title }}</a></h3>
        <p class="muted desc">{{ recipe().description }}</p>
        <div class="meta">
          <app-stars [value]="recipe().averageRating" [count]="recipe().ratingCount" />
          <span class="muted">⏱ {{ recipe().totalMinutes }} min · ♥ {{ recipe().likeCount }}</span>
        </div>
        <div class="by muted">by {{ recipe().authorName }} · {{ recipe().difficulty }}</div>
      </div>
    </article>
  `,
  styles: `
    .rc { overflow: hidden; display: flex; flex-direction: column; height: 100%; }
    .rc:hover { transform: translateY(-4px); box-shadow: var(--shadow-hover); }
    .media { position: relative; display: block; aspect-ratio: 4 / 3; background: var(--bg-alt); overflow: hidden; }
    .media img { width: 100%; height: 100%; object-fit: cover; transition: transform 0.4s ease; }
    .rc:hover .media img { transform: scale(1.05); }
    .badge { position: absolute; top: 0.7rem; left: 0.7rem; background: var(--primary); color: var(--on-primary); font-size: 0.75rem; font-weight: 700; padding: 0.2rem 0.65rem; border-radius: 999px; }
    .body { padding: 1rem 1.15rem 1.2rem; display: flex; flex-direction: column; gap: 0.35rem; flex: 1; }
    .tags { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    h3 { margin: 0.2rem 0 0; font-size: 1.2rem; }
    h3 a { color: var(--text); text-decoration: none; }
    h3 a:hover { color: var(--primary); }
    .desc { margin: 0; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; font-size: 0.93rem; }
    .meta { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.4rem; margin-top: auto; padding-top: 0.5rem; font-size: 0.85rem; }
    .by { font-size: 0.8rem; }
  `,
})
export class RecipeCard {
  readonly recipe = input.required<RecipeSummary>();
}

@Component({
  selector: 'app-pager',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalPages() > 1) {
      <nav class="pager" aria-label="Pagination">
        <button class="btn btn-ghost btn-sm" type="button" [disabled]="page() <= 1" (click)="go.emit(page() - 1)">← Previous</button>
        <span class="muted">Page {{ page() }} of {{ totalPages() }}</span>
        <button class="btn btn-ghost btn-sm" type="button" [disabled]="page() >= totalPages()" (click)="go.emit(page() + 1)">Next →</button>
      </nav>
    }
  `,
  styles: `.pager { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 2rem; }`,
})
export class Pager {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly go = output<number>();
}
