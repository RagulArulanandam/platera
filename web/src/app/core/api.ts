import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdminComment, AdminUser, AuthResponse, Comment, Dashboard, LikeState, Lookup, Paged, ProblemDetails,
  RatingState, RecipeDetail, RecipeQuery, RecipeSummary, SaveRecipeRequest, User,
} from './models';

/** Turns an API ProblemDetails (or network failure) into a readable message. */
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Cannot reach the server. Please check your connection.';
    const p = err.error as ProblemDetails | null;
    if (p?.errors) {
      const first = Object.values(p.errors).flat()[0];
      if (first) return first;
    }
    if (p?.detail) return p.detail;
    if (p?.title) return p.title;
    if (err.status === 429) return 'Too many requests. Please slow down.';
  }
  return 'Something went wrong. Please try again.';
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  // --- auth ---
  register(email: string, password: string, displayName: string) {
    return this.http.post<AuthResponse>('/api/auth/register', { email, password, displayName });
  }
  login(email: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password });
  }
  refresh(refreshToken: string) {
    return this.http.post<AuthResponse>('/api/auth/refresh', { refreshToken });
  }
  logout(refreshToken: string) {
    return this.http.post<void>('/api/auth/logout', { refreshToken });
  }
  me() {
    return this.http.get<User>('/api/auth/me');
  }

  // --- recipes ---
  recipes(q: RecipeQuery): Observable<Paged<RecipeSummary>> {
    return this.http.get<Paged<RecipeSummary>>('/api/recipes', { params: toParams(q) });
  }
  featured(take = 6) {
    return this.http.get<RecipeSummary[]>('/api/recipes/featured', { params: { take } });
  }
  trending(take = 6) {
    return this.http.get<RecipeSummary[]>('/api/recipes/trending', { params: { take } });
  }
  mine(page = 1) {
    return this.http.get<Paged<RecipeSummary>>('/api/recipes/mine', { params: { page, pageSize: 12 } });
  }
  liked(page = 1) {
    return this.http.get<Paged<RecipeSummary>>('/api/recipes/liked', { params: { page, pageSize: 12 } });
  }
  recipe(id: string) {
    return this.http.get<RecipeDetail>(`/api/recipes/${id}`);
  }
  createRecipe(body: SaveRecipeRequest) {
    return this.http.post<RecipeDetail>('/api/recipes', body);
  }
  updateRecipe(id: string, body: SaveRecipeRequest) {
    return this.http.put<RecipeDetail>(`/api/recipes/${id}`, body);
  }
  deleteRecipe(id: string) {
    return this.http.delete<void>(`/api/recipes/${id}`);
  }
  uploadImage(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ url: string }>('/api/uploads/images', form);
  }

  // --- lookups ---
  categories() {
    return this.http.get<Lookup[]>('/api/lookups/categories');
  }
  cuisines() {
    return this.http.get<Lookup[]>('/api/lookups/cuisines');
  }

  // --- interactions ---
  toggleLike(id: string) {
    return this.http.post<LikeState>(`/api/recipes/${id}/like`, {});
  }
  rate(id: string, stars: number) {
    return this.http.put<RatingState>(`/api/recipes/${id}/rating`, { stars });
  }
  comments(id: string, page = 1) {
    return this.http.get<Paged<Comment>>(`/api/recipes/${id}/comments`, { params: { page, pageSize: 10 } });
  }
  addComment(id: string, text: string, photoUrl: string | null) {
    return this.http.post<Comment>(`/api/recipes/${id}/comments`, { text, photoUrl });
  }
  deleteComment(commentId: string) {
    return this.http.delete<void>(`/api/comments/${commentId}`);
  }

  // --- admin ---
  dashboard() {
    return this.http.get<Dashboard>('/api/admin/dashboard');
  }
  adminUsers(search: string, page = 1) {
    return this.http.get<Paged<AdminUser>>('/api/admin/users', { params: { search, page, pageSize: 15 } });
  }
  setUserActive(id: string, isActive: boolean) {
    return this.http.put<void>(`/api/admin/users/${id}/active`, { isActive });
  }
  setUserRole(id: string, role: string) {
    return this.http.put<void>(`/api/admin/users/${id}/role`, { role });
  }
  setFeatured(id: string, isFeatured: boolean) {
    return this.http.put<void>(`/api/admin/recipes/${id}/featured`, { isFeatured });
  }
  adminComments(hiddenOnly: boolean, page = 1) {
    return this.http.get<Paged<AdminComment>>('/api/admin/comments', { params: { hiddenOnly, page, pageSize: 15 } });
  }
  setCommentHidden(id: string, isHidden: boolean) {
    return this.http.put<void>(`/api/admin/comments/${id}/hidden`, { isHidden });
  }
  adminDeleteComment(id: string) {
    return this.http.delete<void>(`/api/admin/comments/${id}`);
  }
  saveLookup(kind: 'categories' | 'cuisines', name: string, id?: number) {
    return id
      ? this.http.put<Lookup>(`/api/admin/${kind}/${id}`, { name })
      : this.http.post<Lookup>(`/api/admin/${kind}`, { name });
  }
  deleteLookup(kind: 'categories' | 'cuisines', id: number) {
    return this.http.delete<void>(`/api/admin/${kind}/${id}`);
  }
}

function toParams(q: object): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(q)) {
    if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
  }
  return p;
}
