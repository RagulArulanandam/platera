import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards';

export const routes: Routes = [
  { path: '', title: 'Platera – Every dish has a story', loadComponent: () => import('./pages/home').then((m) => m.HomePage) },
  { path: 'recipes', title: 'Browse recipes – Platera', loadComponent: () => import('./pages/browse').then((m) => m.BrowsePage) },
  {
    path: 'recipes/new', title: 'Share a recipe – Platera', canActivate: [authGuard],
    loadComponent: () => import('./pages/recipe-form').then((m) => m.RecipeFormPage),
  },
  {
    path: 'recipes/:id/edit', title: 'Edit recipe – Platera', canActivate: [authGuard],
    loadComponent: () => import('./pages/recipe-form').then((m) => m.RecipeFormPage),
  },
  { path: 'recipes/:id', title: 'Recipe – Platera', loadComponent: () => import('./pages/recipe-detail').then((m) => m.RecipeDetailPage) },
  {
    path: 'profile', title: 'My kitchen – Platera', canActivate: [authGuard],
    loadComponent: () => import('./pages/profile').then((m) => m.ProfilePage),
  },
  {
    path: 'admin', title: 'Admin – Platera', canActivate: [adminGuard],
    loadComponent: () => import('./pages/admin').then((m) => m.AdminPage),
  },
  { path: 'login', title: 'Log in – Platera', loadComponent: () => import('./pages/auth').then((m) => m.LoginPage) },
  { path: 'register', title: 'Join Platera', loadComponent: () => import('./pages/auth').then((m) => m.RegisterPage) },
  { path: '**', title: 'Not found – Platera', loadComponent: () => import('./pages/not-found').then((m) => m.NotFoundPage) },
];
