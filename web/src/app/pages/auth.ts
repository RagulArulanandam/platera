import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { errorMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { Logo } from '../shared/ui';

const authStyles = `
  .wrap { max-width: 440px; margin: 3rem auto; padding: 2rem; }
  .head { text-align: center; margin-bottom: 1rem; }
  .btn { width: 100%; margin-top: 0.5rem; }
  .alt { text-align: center; margin: 1rem 0 0; }
`;

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, Logo],
  styles: authStyles,
  template: `
    <div class="card wrap">
      <div class="head"><app-logo [wordmark]="false" [size]="52" /><h1 style="font-size:1.8rem">Welcome back</h1></div>
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field"><label for="email">Email</label><input id="email" type="email" formControlName="email" autocomplete="email" /></div>
        <div class="field"><label for="password">Password</label><input id="password" type="password" formControlName="password" autocomplete="current-password" /></div>
        @if (error()) { <p class="error-text" role="alert">{{ error() }}</p> }
        <button class="btn" type="submit" [disabled]="busy() || form.invalid">{{ busy() ? 'Signing in…' : 'Log in' }}</button>
      </form>
      <p class="alt muted">New to Platera? <a routerLink="/register">Create an account</a></p>
    </div>
  `,
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit() {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.error.set('');
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('returnUrl') || '/'),
      error: (e) => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }
}

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, Logo],
  styles: authStyles,
  template: `
    <div class="card wrap">
      <div class="head"><app-logo [wordmark]="false" [size]="52" /><h1 style="font-size:1.8rem">Join Platera</h1></div>
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field"><label for="name">Display name</label><input id="name" formControlName="displayName" autocomplete="nickname" maxlength="80" /></div>
        <div class="field"><label for="email">Email</label><input id="email" type="email" formControlName="email" autocomplete="email" /></div>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" type="password" formControlName="password" autocomplete="new-password" aria-describedby="pw-help" />
          <small id="pw-help" class="muted">At least 8 characters, with upper and lower case letters, a digit and a symbol.</small>
        </div>
        @if (error()) { <p class="error-text" role="alert">{{ error() }}</p> }
        <button class="btn" type="submit" [disabled]="busy() || form.invalid">{{ busy() ? 'Creating…' : 'Create account' }}</button>
      </form>
      <p class="alt muted">Already cooking with us? <a routerLink="/login">Log in</a></p>
    </div>
  `,
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit() {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.error.set('');
    const { email, password, displayName } = this.form.getRawValue();
    this.auth.register(email, password, displayName).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (e) => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }
}
