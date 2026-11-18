import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { Api } from './api';
import { AuthResponse, User } from './models';

const KEY = 'platera.auth';

interface Stored {
  accessToken: string;
  refreshToken: string;
  user: User;
}

function read(): Stored | null {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as Stored) : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly state = signal<Stored | null>(read());
  private refreshing$: Observable<string> | null = null;

  readonly user = computed(() => this.state()?.user ?? null);
  readonly isAuthenticated = computed(() => this.state() !== null);
  readonly isAdmin = computed(() => this.state()?.user.roles.includes('Admin') ?? false);

  get accessToken(): string | null {
    return this.state()?.accessToken ?? null;
  }
  get refreshToken(): string | null {
    return this.state()?.refreshToken ?? null;
  }

  login(email: string, password: string) {
    return this.api.login(email, password).pipe(tap((r) => this.store(r)));
  }

  register(email: string, password: string, displayName: string) {
    return this.api.register(email, password, displayName).pipe(tap((r) => this.store(r)));
  }

  /** Single-flight refresh: concurrent 401s share one request. Emits the new access token. */
  refresh(): Observable<string> {
    const token = this.refreshToken;
    if (!token) throw new Error('No refresh token');
    if (!this.refreshing$) {
      this.refreshing$ = this.api.refresh(token).pipe(
        tap((r) => this.store(r)),
        map((r) => r.accessToken),
        finalize(() => (this.refreshing$ = null)),
        shareReplay(1),
      );
    }
    return this.refreshing$;
  }

  logout(navigate = true) {
    const token = this.refreshToken;
    if (token) this.api.logout(token).subscribe({ error: () => undefined });
    this.clear();
    if (navigate) void this.router.navigateByUrl('/');
  }

  clear() {
    this.state.set(null);
    try {
      localStorage.removeItem(KEY);
    } catch {
      /* storage unavailable */
    }
  }

  private store(r: AuthResponse) {
    const s: Stored = { accessToken: r.accessToken, refreshToken: r.refreshToken, user: r.user };
    this.state.set(s);
    try {
      localStorage.setItem(KEY, JSON.stringify(s));
    } catch {
      /* storage unavailable */
    }
  }
}
