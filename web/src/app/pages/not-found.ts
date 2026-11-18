import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="narrow page" style="text-align:center">
      <h1>Page not found</h1>
      <p class="muted">This dish isn't on the menu. Let's get you back to the kitchen.</p>
      <a class="btn" routerLink="/">Back home</a>
    </div>
  `,
})
export class NotFoundPage {}
