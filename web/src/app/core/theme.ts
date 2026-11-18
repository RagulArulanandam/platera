import { Injectable, effect, signal } from '@angular/core';

export type Theme = 'light' | 'dark';
const KEY = 'platera.theme';

function initial(): Theme {
  try {
    const saved = localStorage.getItem(KEY);
    if (saved === 'light' || saved === 'dark') return saved;
  } catch {
    /* storage unavailable */
  }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(initial());

  constructor() {
    effect(() => {
      const t = this.theme();
      document.documentElement.setAttribute('data-theme', t);
      try {
        localStorage.setItem(KEY, t);
      } catch {
        /* storage unavailable */
      }
    });
  }

  toggle() {
    this.theme.update((t) => (t === 'dark' ? 'light' : 'dark'));
  }
}
