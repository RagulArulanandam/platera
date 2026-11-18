import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth';
import { ToastService } from './toast';

const AUTH_URLS = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh'];

function withToken(req: HttpRequest<unknown>, token: string | null) {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

/** Adds the JWT, and transparently refreshes once on 401 before retrying. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isApi = req.url.startsWith('/api/');
  const isAuthCall = AUTH_URLS.some((u) => req.url.startsWith(u));
  if (!isApi || isAuthCall) return next(req);

  return next(withToken(req, auth.accessToken)).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401 && auth.refreshToken) {
        return auth.refresh().pipe(
          switchMap((token) => next(withToken(req, token))),
          catchError((refreshErr: unknown) => {
            auth.clear();
            void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
            return throwError(() => refreshErr);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};

/** Surfaces server/network failures as toasts; 4xx are left to the calling component. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        if (err.status === 0) toast.show('Cannot reach the server.', 'error');
        else if (err.status >= 500) toast.show('The server hit a problem. Please try again.', 'error');
        else if (err.status === 429) toast.show('Too many requests. Please slow down.', 'error');
      }
      return throwError(() => err);
    }),
  );
};
