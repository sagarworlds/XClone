import { Routes, UrlMatcher } from '@angular/router';
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

// /profile/:username/followers and /profile/:username/following share one page (and one route, so the page is kept
// when switching between the two lists)
export const followListMatcher: UrlMatcher = (segments) =>
  segments.length === 3 && segments[0].path === 'profile' && (segments[2].path === 'followers' || segments[2].path === 'following')
    ? { consumed: segments, posParams: { username: segments[1], list: segments[2] } }
    : null;

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
    path: 'hashtag/:tag',
    loadComponent: () => import('./components/hashtag').then(m => m.HashtagComponent),
    canActivate: [authGuard]
  },
  {
    path: 'search',
    loadComponent: () => import('./components/search').then(m => m.SearchComponent),
    canActivate: [authGuard]
  },
  {
    path: 'notifications',
    loadComponent: () => import('./components/notifications').then(m => m.NotificationsComponent),
    canActivate: [authGuard]
  },
  {
    matcher: followListMatcher,
    loadComponent: () => import('./components/follow-list').then(m => m.FollowListComponent),
    canActivate: [authGuard]
  },
  {
    path: 'profile/:username',
    loadComponent: () => import('./components/profile').then(m => m.ProfileComponent),
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: 'home' }
];
