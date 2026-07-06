import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { User, Post, AuthResponse } from '../models/types';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  // Signals for global state
  readonly currentUser = signal<User | null>(null);
  readonly isAuthenticated = signal<boolean>(false);

  constructor() {
    this.loadSession();
  }

  private loadSession(): void {
    const token = localStorage.getItem('token');
    const savedUser = localStorage.getItem('user');
    if (token && savedUser) {
      try {
        this.currentUser.set(JSON.parse(savedUser));
        this.isAuthenticated.set(true);
      } catch {
        this.logout();
      }
    }
  }

  // Auth Methods
  register(data: any): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/register`, data)
      .pipe(tap((res) => this.handleAuthSuccess(res)));
  }

  login(data: any): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/login`, data)
      .pipe(tap((res) => this.handleAuthSuccess(res)));
  }

  logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    this.router.navigate(['/login']);
  }

  private handleAuthSuccess(res: AuthResponse): void {
    localStorage.setItem('token', res.token);
    localStorage.setItem('user', JSON.stringify(res.user));
    this.currentUser.set(res.user);
    this.isAuthenticated.set(true);
  }

  // Posts Methods
  getFeed(skip = 0, take = 10): Observable<Post[]> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.http.get<Post[]>(`${this.baseUrl}/posts/feed`, { params });
  }

  getUserPosts(userId: number, skip = 0, take = 10): Observable<Post[]> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.http.get<Post[]>(`${this.baseUrl}/posts/user/${userId}`, { params });
  }

  createPost(content: string, mediaUrls: string[] = []): Observable<Post> {
    return this.http.post<Post>(`${this.baseUrl}/posts`, { content, mediaUrls });
  }

  deletePost(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/posts/${id}`);
  }

  // Likes Methods
  likePost(postId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/likes/${postId}`, {});
  }

  unlikePost(postId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/likes/${postId}`);
  }

  // Follows Methods
  followUser(userId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/follows/${userId}`, {});
  }

  unfollowUser(userId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/follows/${userId}`);
  }

  getFollowers(userId: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/follows/${userId}/followers`);
  }

  getFollowing(userId: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/follows/${userId}/following`);
  }

  // Users Methods
  getCurrentUserProfile(): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/users/profile`).pipe(
      tap((user) => {
        localStorage.setItem('user', JSON.stringify(user));
        this.currentUser.set(user);
      }),
    );
  }

  getUserProfileByUsername(username: string): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/users/profile/${username}`);
  }

  updateProfile(data: { displayName: string; bio: string; avatarUrl: string }): Observable<User> {
    return this.http.put<User>(`${this.baseUrl}/users/profile`, data).pipe(
      tap((user) => {
        localStorage.setItem('user', JSON.stringify(user));
        this.currentUser.set(user);
      }),
    );
  }

  searchUsers(query: string): Observable<User[]> {
    const params = new HttpParams().set('query', query);
    return this.http.get<User[]>(`${this.baseUrl}/users/search`, { params });
  }
}
