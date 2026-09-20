import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { User, Post, AuthResponse, AppNotification, MediaUpload, Page, TrendingHashtag } from '../models/types';
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
    // The auth endpoints return a flat payload; the fields not included default until the profile is loaded.
    const user: User = {
      id: res.id,
      username: res.username,
      email: res.email,
      displayName: res.displayName,
      avatarUrl: res.avatarUrl,
      bio: '',
      createdAt: '',
      followersCount: 0,
      followingCount: 0,
      postsCount: 0,
      isFollowed: false,
    };
    localStorage.setItem('token', res.token);
    localStorage.setItem('user', JSON.stringify(user));
    this.currentUser.set(user);
    this.isAuthenticated.set(true);
  }

  // Posts Methods
  // The lists below are read a page at a time: pass the previous page's nextCursor (null for the first page)
  private pageParams(cursor: string | null, take: number): HttpParams {
    const params = new HttpParams().set('take', take);
    return cursor ? params.set('cursor', cursor) : params;
  }

  getFeed(cursor: string | null = null, take = 20): Observable<Page<Post>> {
    return this.http.get<Page<Post>>(`${this.baseUrl}/posts/feed`, { params: this.pageParams(cursor, take) });
  }

  getUserPosts(userId: number, cursor: string | null = null, take = 20): Observable<Page<Post>> {
    return this.http.get<Page<Post>>(`${this.baseUrl}/posts/user/${userId}`, { params: this.pageParams(cursor, take) });
  }

  getPost(id: number): Observable<Post> {
    return this.http.get<Post>(`${this.baseUrl}/posts/${id}`);
  }

  getReplies(postId: number, cursor: string | null = null, take = 20): Observable<Page<Post>> {
    return this.http.get<Page<Post>>(`${this.baseUrl}/posts/${postId}/replies`, { params: this.pageParams(cursor, take) });
  }

  getUserReplies(userId: number, cursor: string | null = null, take = 20): Observable<Page<Post>> {
    return this.http.get<Page<Post>>(`${this.baseUrl}/posts/user/${userId}/replies`, { params: this.pageParams(cursor, take) });
  }

  /** Posts and replies that use the hashtag (without the #), newest first. */
  getHashtagPosts(tag: string, cursor: string | null = null, take = 20): Observable<Page<Post>> {
    return this.http.get<Page<Post>>(`${this.baseUrl}/posts/hashtag/${encodeURIComponent(tag)}`, { params: this.pageParams(cursor, take) });
  }

  /** The hashtags most used by posts of the last week. */
  getTrendingHashtags(take = 5): Observable<TrendingHashtag[]> {
    return this.http.get<TrendingHashtag[]>(`${this.baseUrl}/hashtags/trending`, { params: new HttpParams().set('take', take) });
  }

  createReply(postId: number, content: string, mediaUrls: string[] = []): Observable<Post> {
    return this.http.post<Post>(`${this.baseUrl}/posts/${postId}/replies`, { content, mediaUrls });
  }

  /** Uploads one image; the answer's url is what a post's mediaUrls take. */
  uploadMedia(file: File): Observable<MediaUpload> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<MediaUpload>(`${this.baseUrl}/media`, form);
  }

  createPost(content: string, mediaUrls: string[] = []): Observable<Post> {
    return this.http.post<Post>(`${this.baseUrl}/posts`, { content, mediaUrls });
  }

  deletePost(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/posts/${id}`);
  }

  // Likes Methods (the API exposes a single toggle endpoint)
  likePost(postId: number): Observable<{ liked: boolean }> {
    return this.toggleLike(postId);
  }

  unlikePost(postId: number): Observable<{ liked: boolean }> {
    return this.toggleLike(postId);
  }

  private toggleLike(postId: number): Observable<{ liked: boolean }> {
    return this.http.post<{ liked: boolean }>(`${this.baseUrl}/likes/toggle/${postId}`, {});
  }

  // Retweets Methods (single toggle endpoint)
  toggleRetweet(postId: number): Observable<{ retweeted: boolean }> {
    return this.http.post<{ retweeted: boolean }>(`${this.baseUrl}/retweets/toggle/${postId}`, {});
  }

  // Follows Methods (the API exposes a single toggle endpoint)
  followUser(userId: number): Observable<{ followed: boolean }> {
    return this.toggleFollow(userId);
  }

  unfollowUser(userId: number): Observable<{ followed: boolean }> {
    return this.toggleFollow(userId);
  }

  private toggleFollow(userId: number): Observable<{ followed: boolean }> {
    return this.http.post<{ followed: boolean }>(`${this.baseUrl}/follows/toggle/${userId}`, {});
  }

  getFollowers(userId: number, cursor: string | null = null, take = 20): Observable<Page<User>> {
    return this.http.get<Page<User>>(`${this.baseUrl}/users/${userId}/followers`, { params: this.pageParams(cursor, take) });
  }

  getFollowing(userId: number, cursor: string | null = null, take = 20): Observable<Page<User>> {
    return this.http.get<Page<User>>(`${this.baseUrl}/users/${userId}/following`, { params: this.pageParams(cursor, take) });
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

  // Notifications
  getNotifications(cursor: string | null = null, take = 20): Observable<Page<AppNotification>> {
    return this.http.get<Page<AppNotification>>(`${this.baseUrl}/notifications`, { params: this.pageParams(cursor, take) });
  }

  getUnreadNotificationCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/notifications/unread-count`);
  }

  markNotificationsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/notifications/read-all`, {});
  }

  getSuggestions(take = 4): Observable<User[]> {
    const params = new HttpParams().set('take', take);
    return this.http.get<User[]>(`${this.baseUrl}/users/suggestions`, { params });
  }

  searchUsers(query: string): Observable<User[]> {
    const params = new HttpParams().set('query', query);
    return this.http.get<User[]>(`${this.baseUrl}/users/search`, { params });
  }
}
