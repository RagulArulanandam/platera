import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api';
import { Difficulty, Lookup, RecipeImage, SaveRecipeRequest } from '../core/models';
import { ToastService } from '../core/toast';

@Component({
  selector: 'app-recipe-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="narrow page">
      <h1>{{ id() ? 'Edit recipe' : 'Share a recipe' }}</h1>
      @if (loading()) { <p class="muted">Loading…</p> } @else {
      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        <section class="card card-pad">
          <h2>The basics</h2>
          <div class="field"><label for="title">Title</label><input id="title" formControlName="title" maxlength="160" /></div>
          <div class="field"><label for="desc">Short description</label><textarea id="desc" formControlName="description" maxlength="2000"></textarea></div>
          <div class="row">
            <div class="field"><label for="cuisine">Cuisine</label>
              <select id="cuisine" formControlName="cuisineId">
                <option [ngValue]="0" disabled>Choose…</option>
                @for (c of cuisines(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
              </select></div>
            <div class="field"><label for="category">Category</label>
              <select id="category" formControlName="categoryId">
                <option [ngValue]="0" disabled>Choose…</option>
                @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
              </select></div>
          </div>
          <div class="row four">
            <div class="field"><label for="prep">Prep (min)</label><input id="prep" type="number" min="0" formControlName="prepMinutes" /></div>
            <div class="field"><label for="cook">Cook (min)</label><input id="cook" type="number" min="0" formControlName="cookMinutes" /></div>
            <div class="field"><label for="serves">Servings</label><input id="serves" type="number" min="1" formControlName="servings" /></div>
            <div class="field"><label for="diff">Difficulty</label>
              <select id="diff" formControlName="difficulty">@for (d of difficulties; track d) { <option [value]="d">{{ d }}</option> }</select></div>
          </div>
          <div class="field"><label for="tags">Tags</label><input id="tags" formControlName="tags" placeholder="vegan, quick, street-food" />
            <small class="muted">Comma separated, up to 10.</small></div>
        </section>

        <section class="card card-pad">
          <h2>Photos <span class="muted small">({{ images().length }}/5)</span></h2>
          <div class="imgs">
            @for (img of images(); track img.url; let i = $index) {
              <figure>
                <img [src]="img.url" [alt]="img.altText || 'Recipe photo ' + (i + 1)" />
                <input [value]="img.altText" (input)="setAlt(i, $any($event.target).value)" placeholder="Describe the photo" [attr.aria-label]="'Alt text for photo ' + (i + 1)" />
                <button type="button" class="btn btn-ghost btn-sm" (click)="removeImage(i)">Remove</button>
              </figure>
            }
          </div>
          @if (images().length < 5) {
            <label class="btn btn-ghost file">{{ uploading() ? 'Uploading…' : '＋ Add photo' }}
              <input class="sr-only" type="file" accept="image/png,image/jpeg,image/webp,image/gif" (change)="upload($event)" [disabled]="uploading()" />
            </label>
            <small class="muted"> JPEG, PNG, GIF or WebP · max 5 MB</small>
          }
        </section>

        <section class="card card-pad">
          <h2>Ingredients</h2>
          <div formArrayName="ingredients">
            @for (g of ingredients.controls; track g; let i = $index) {
              <div class="line" [formGroupName]="i">
                <input formControlName="quantity" placeholder="Qty" [attr.aria-label]="'Quantity ' + (i + 1)" class="qty" />
                <input formControlName="name" placeholder="Ingredient" [attr.aria-label]="'Ingredient ' + (i + 1)" />
                <button type="button" class="btn btn-ghost btn-sm" (click)="remove(ingredients, i)" [disabled]="ingredients.length <= 1" aria-label="Remove ingredient">✕</button>
              </div>
            }
          </div>
          <button type="button" class="btn btn-ghost btn-sm" (click)="addIngredient()">＋ Add ingredient</button>
        </section>

        <section class="card card-pad">
          <h2>Method</h2>
          <div formArrayName="steps">
            @for (c of steps.controls; track c; let i = $index) {
              <div class="line">
                <span class="num">{{ i + 1 }}</span>
                <textarea [formControlName]="i" rows="2" [attr.aria-label]="'Step ' + (i + 1)"></textarea>
                <button type="button" class="btn btn-ghost btn-sm" (click)="remove(steps, i)" [disabled]="steps.length <= 1" aria-label="Remove step">✕</button>
              </div>
            }
          </div>
          <button type="button" class="btn btn-ghost btn-sm" (click)="addStep()">＋ Add step</button>
        </section>

        @if (error()) { <p class="error-text" role="alert">{{ error() }}</p> }
        <div class="submit">
          <a class="btn btn-ghost" [routerLink]="id() ? ['/recipes', id()] : ['/recipes']">Cancel</a>
          <button class="btn" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : id() ? 'Save changes' : 'Publish recipe' }}</button>
        </div>
      </form>
      }
    </div>
  `,
  styles: `
    section { margin-bottom: 1.25rem; }
    .row { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; }
    .row.four { grid-template-columns: repeat(4, 1fr); }
    .small { font-size: 0.9rem; font-weight: 400; }
    .imgs { display: flex; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; }
    figure { margin: 0; width: 170px; display: grid; gap: 0.4rem; }
    figure img { width: 170px; height: 128px; object-fit: cover; border-radius: var(--radius-sm); }
    .line { display: flex; gap: 0.5rem; align-items: flex-start; margin-bottom: 0.5rem; }
    .qty { max-width: 130px; }
    textarea { min-height: 60px; }
    .num { font-family: var(--font-display); font-weight: 700; color: var(--primary); padding-top: 0.55rem; min-width: 1.4rem; }
    .file { cursor: pointer; }
    .submit { display: flex; justify-content: flex-end; gap: 0.75rem; }
    @media (max-width: 600px) { .row, .row.four { grid-template-columns: 1fr 1fr; } }
  `,
})
export class RecipeFormPage {
  /** Present on /recipes/:id/edit */
  readonly id = input<string>();

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly difficulties: Difficulty[] = ['Easy', 'Medium', 'Hard'];
  protected readonly cuisines = signal<Lookup[]>([]);
  protected readonly categories = signal<Lookup[]>([]);
  protected readonly images = signal<RecipeImage[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly uploading = signal(false);
  protected readonly error = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    cuisineId: [0, [Validators.min(1)]],
    categoryId: [0, [Validators.min(1)]],
    prepMinutes: [10, [Validators.required, Validators.min(0)]],
    cookMinutes: [20, [Validators.required, Validators.min(0)]],
    servings: [2, [Validators.required, Validators.min(1)]],
    difficulty: ['Easy' as Difficulty],
    tags: [''],
    ingredients: this.fb.array([this.ingredientGroup()]),
    steps: this.fb.array([this.fb.nonNullable.control('', Validators.required)]),
  });

  protected get ingredients() { return this.form.controls.ingredients; }
  protected get steps() { return this.form.controls.steps; }

  constructor() {
    this.api.cuisines().subscribe((r) => this.cuisines.set(r));
    this.api.categories().subscribe((r) => this.categories.set(r));

    effect(() => {
      const id = this.id();
      if (!id) return;
      this.loading.set(true);
      this.api.recipe(id).subscribe({
        next: (r) => {
          this.ingredients.clear();
          r.ingredients.forEach((i) => this.ingredients.push(this.ingredientGroup(i.quantity, i.name)));
          this.steps.clear();
          r.steps.forEach((s) => this.steps.push(this.fb.nonNullable.control(s, Validators.required)));
          this.form.patchValue({
            title: r.title, description: r.description, cuisineId: r.cuisineId, categoryId: r.categoryId,
            prepMinutes: r.prepMinutes, cookMinutes: r.cookMinutes, servings: r.servings, difficulty: r.difficulty,
            tags: r.tags.join(', '),
          });
          this.images.set(r.images);
          this.loading.set(false);
        },
        error: (e) => { this.toast.show(errorMessage(e), 'error'); void this.router.navigateByUrl('/profile'); },
      });
    });
  }

  private ingredientGroup(quantity = '', name = '') {
    return this.fb.nonNullable.group({ quantity: [quantity], name: [name, Validators.required] });
  }

  protected addIngredient() { this.ingredients.push(this.ingredientGroup()); }
  protected addStep() { this.steps.push(this.fb.nonNullable.control('', Validators.required)); }
  protected remove(arr: FormArray, i: number) { arr.removeAt(i); }

  protected upload(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) { this.toast.show('Photos must be 5 MB or smaller.', 'error'); return; }
    this.uploading.set(true);
    this.api.uploadImage(file).subscribe({
      next: (r) => { this.images.update((l) => [...l, { url: r.url, altText: '' }]); this.uploading.set(false); },
      error: (err) => { this.toast.show(errorMessage(err), 'error'); this.uploading.set(false); },
    });
  }

  protected setAlt(i: number, v: string) {
    this.images.update((l) => l.map((x, n) => (n === i ? { ...x, altText: v } : x)));
  }
  protected removeImage(i: number) { this.images.update((l) => l.filter((_, n) => n !== i)); }

  protected save() {
    this.error.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid) { this.error.set('Please complete the title, description, cuisine, category, ingredients and steps.'); return; }
    if (this.images().length === 0) { this.error.set('Add at least one photo.'); return; }

    const v = this.form.getRawValue();
    const body: SaveRecipeRequest = {
      title: v.title, description: v.description, cuisineId: v.cuisineId, categoryId: v.categoryId,
      prepMinutes: +v.prepMinutes, cookMinutes: +v.cookMinutes, servings: +v.servings, difficulty: v.difficulty,
      ingredients: v.ingredients.filter((i) => i.name.trim()),
      steps: v.steps.filter((s) => s.trim()),
      images: this.images(),
      tags: v.tags.split(',').map((t) => t.trim()).filter(Boolean).slice(0, 10),
    };
    this.saving.set(true);
    const req = this.id() ? this.api.updateRecipe(this.id()!, body) : this.api.createRecipe(body);
    req.subscribe({
      next: (r) => { this.toast.show('Recipe saved!', 'success'); void this.router.navigate(['/recipes', r.id]); },
      error: (e) => { this.error.set(errorMessage(e)); this.saving.set(false); },
    });
  }
}
