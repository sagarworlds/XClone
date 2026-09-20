import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { ApiService } from './services/api.service';
import { Router } from '@angular/router';

const authGuard = () => {
  const api = inject(ApiService);
  const router = inject(Router);
  if (api.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/login']);
};

const guestGuard = () => {
  const api = inject(ApiService);
  const router = inject(Router);
  if (!api.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/home']);
};

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./components/login').then(m => m.LoginComponent),
    canActivate: [guestGuard]
  },
  {
    path: 'register',
    loadComponent: () => import('./components/register').then(m => m.RegisterComponent),
    canActivate: [guestGuard]
  },
  {
    path: 'home',
    loadComponent: () => import('./components/feed').then(m => m.FeedComponent),
    canActivate: [authGuard]
  },
  {
    path: 'post/:id',
    loadComponent: () => import('./components/post-detail').then(m => m.PostDetailComponent),
    canActivate: [authGuard]
  },
  {
    path: 'notifications',
    loadComponent: () => import('./components/notifications').then(m => m.NotificationsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'profile/:username',
    loadComponent: () => import('./components/profile').then(m => m.ProfileComponent),
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: 'home' }
];
