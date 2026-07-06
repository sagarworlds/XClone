import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { Post, User } from '../models/types';

@Component({
  selector: 'app-feed',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="app-container">
      <!-- Left Sidebar Navigation -->
      <aside class="sidebar">
        <div>
          <div class="logo-container">
            <svg viewBox="0 0 24 24" aria-hidden="true" style="width: 32px; height: 32px; fill: currentColor;">
              <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"></path>
            </svg>
          </div>
          
          <nav class="nav-links">
            <a routerLink="/home" class="nav-item active">
              <span class="material-symbols-outlined">home</span>
              <span>Home</span>
            </a>
            @if (currentUser()) {
              <a [routerLink]="['/profile', currentUser()?.username]" class="nav-item">
                <span class="material-symbols-outlined">person</span>
                <span>Profile</span>
              </a>
            }
            <button (click)="logout()" class="nav-item logout-btn" style="background: transparent; width: 100%; text-align: left;">
              <span class="material-symbols-outlined">logout</span>
              <span>Log out</span>
            </button>
          </nav>
        </div>

        @if (currentUser()) {
          <div class="user-profile-summary" [routerLink]="['/profile', currentUser()?.username]">
            <img [src]="currentUser()?.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
            <div class="user-info">
              <span class="display-name">{{ currentUser()?.displayName }}</span>
              <span class="username">@{{ currentUser()?.username }}</span>
            </div>
          </div>
        }
      </aside>

      <!-- Main Feed Area -->
      <main class="main-content">
        <header class="header">
          <h2>Home</h2>
        </header>

        <!-- Post Creator/Composer -->
        <div class="composer-container">
          @if (currentUser()) {
            <img [src]="currentUser()?.avatarUrl || defaultAvatar" alt="Avatar" class="avatar composer-avatar" />
          }
          <div class="composer-body">
            <textarea 
              [(ngModel)]="newPostContent" 
              placeholder="What's happening?!" 
              rows="3"
              maxlength="280"
              class="composer-textarea"
            ></textarea>
            
            <div class="composer-footer">
              <span class="char-counter" [class.warning]="charCount() > 250">
                {{ charCount() }}/280
              </span>
              <button 
                [disabled]="!newPostContent.trim() || posting()" 
                (click)="submitPost()" 
                class="publish-btn"
              >
                @if (posting()) { Posting... } @else { Post }
              </button>
            </div>
          </div>
        </div>

        <!-- Tweets List -->
        <div class="feed-posts">
          @if (loadingFeed()) {
            <div class="loading-spinner">Loading posts...</div>
          } @else if (posts().length === 0) {
            <div class="empty-feed">
              <h3>Welcome to X!</h3>
              <p>Follow people and start sharing what's on your mind.</p>
            </div>
          } @else {
            @for (post of posts(); track post.id) {
              <div class="post-card">
                <img 
                  [src]="post.user?.avatarUrl || defaultAvatar" 
                  alt="Avatar" 
                  class="avatar post-avatar" 
                  [routerLink]="['/profile', post.user?.username]"
                />
                
                <div class="post-content-container">
                  <div class="post-header">
                    <div class="post-user-info" [routerLink]="['/profile', post.user?.username]">
                      <span class="post-display-name">{{ post.user?.displayName }}</span>
                      <span class="post-username">@{{ post.user?.username }}</span>
                      <span class="post-dot">·</span>
                      <span class="post-time">{{ formatTime(post.createdAt) }}</span>
                    </div>
                    
                    @if (currentUser() && currentUser()?.id === post.userId) {
                      <button class="delete-post-btn" (click)="deletePost(post.id)">
                        <span class="material-symbols-outlined delete-icon">delete</span>
                      </button>
                    }
                  </div>

                  <p class="post-text-content">{{ post.content }}</p>

                  <div class="post-actions">
                    <button class="action-btn comment-btn">
                      <span class="material-symbols-outlined">chat_bubble</span>
                      <span>{{ post.repliesCount || 0 }}</span>
                    </button>
                    <button class="action-btn retweet-btn">
                      <span class="material-symbols-outlined">repeat</span>
                      <span>{{ post.retweetsCount || 0 }}</span>
                    </button>
                    <button 
                      class="action-btn like-btn" 
                      [class.liked]="post.isLiked" 
                      (click)="toggleLike(post)"
                    >
                      <span class="material-symbols-outlined" [class.filled]="post.isLiked">
                        {{ post.isLiked ? 'favorite' : 'favorite' }}
                      </span>
                      <span>{{ post.likesCount }}</span>
                    </button>
                  </div>
                </div>
              </div>
            }
          }
        </div>
      </main>

      <!-- Right Sidebar Widgets -->
      <aside class="widgets">
        <div class="search-box">
          <span class="material-symbols-outlined">search</span>
          <input 
            type="text" 
            [(ngModel)]="searchQuery" 
            (input)="onSearchChange()" 
            placeholder="Search users..." 
          />
        </div>

        @if (searchResults().length > 0) {
          <div class="widget-card">
            <h3>Search Results</h3>
            @for (user of searchResults(); track user.id) {
              <div class="widget-item user-row">
                <div class="user-details" [routerLink]="['/profile', user.username]">
                  <img [src]="user.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
                  <div class="user-info">
                    <span class="display-name">{{ user.displayName }}</span>
                    <span class="username">@{{ user.username }}</span>
                  </div>
                </div>
              </div>
            }
          </div>
        }

        <div class="widget-card">
          <h3>Who to follow</h3>
          @if (loadingSuggestions()) {
            <div style="color: var(--text-secondary);">Loading recommendations...</div>
          } @else if (suggestions().length === 0) {
            <div style="color: var(--text-secondary);">No recommendations found.</div>
          } @else {
            @for (user of suggestions(); track user.id) {
              <div class="widget-item user-row">
                <div class="user-details" [routerLink]="['/profile', user.username]">
                  <img [src]="user.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
                  <div class="user-info">
                    <span class="display-name">{{ user.displayName }}</span>
                    <span class="username">@{{ user.username }}</span>
                  </div>
                </div>
                <button 
                  class="follow-btn" 
                  [class.following]="user.isFollowed"
                  (click)="toggleFollow(user)"
                >
                  {{ user.isFollowed ? 'Following' : 'Follow' }}
                </button>
              </div>
            }
          }
        </div>
      </aside>
    </div>
  `,
  styles: [`
    .composer-container {
      display: flex;
      padding: 16px;
      border-bottom: 1px solid var(--border-color);
    }
    .composer-avatar {
      margin-right: 12px;
    }
    .composer-body {
      flex-grow: 1;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .composer-textarea {
      width: 100%;
      border: none;
      resize: none;
      background: transparent;
      color: var(--text-primary);
      font-size: 1.25rem;
      outline: none;
      padding: 4px 0;
    }
    .composer-textarea::placeholder {
      color: var(--text-secondary);
    }
    .composer-textarea:focus {
      border: none;
    }
    .composer-footer {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 16px;
      border-top: 1px solid var(--border-color);
      padding-top: 12px;
    }
    .char-counter {
      color: var(--text-secondary);
      font-size: 0.85rem;
    }
    .char-counter.warning {
      color: var(--danger-color);
    }
    .publish-btn {
      background-color: var(--accent-color);
      color: #fff;
      font-weight: 700;
      padding: 8px 16px;
      border-radius: 9999px;
      font-size: 0.95rem;
    }
    .publish-btn:hover:not(:disabled) {
      background-color: var(--accent-hover);
    }
    .publish-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    /* Feed Posts */
    .post-card {
      display: flex;
      padding: 16px;
      border-bottom: 1px solid var(--border-color);
      cursor: pointer;
      transition: background-color 0.2s ease;
    }
    .post-card:hover {
      background-color: rgba(255, 255, 255, 0.02);
    }
    .post-avatar {
      margin-right: 12px;
      cursor: pointer;
    }
    .post-content-container {
      flex-grow: 1;
      display: flex;
      flex-direction: column;
    }
    .post-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 4px;
    }
    .post-user-info {
      display: flex;
      align-items: center;
      gap: 4px;
      cursor: pointer;
    }
    .post-user-info:hover .post-display-name {
      text-decoration: underline;
    }
    .post-display-name {
      font-weight: 700;
      color: var(--text-primary);
    }
    .post-username, .post-dot, .post-time {
      color: var(--text-secondary);
      font-size: 0.9rem;
    }
    .delete-post-btn {
      background: transparent;
      color: var(--text-secondary);
      border-radius: 50%;
      padding: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .delete-post-btn:hover {
      background-color: rgba(244, 33, 46, 0.1);
      color: var(--danger-color);
    }
    .delete-icon {
      font-size: 1.2rem;
    }
    .post-text-content {
      font-size: 0.95rem;
      line-height: 1.4;
      white-space: pre-wrap;
      word-break: break-word;
      margin-bottom: 12px;
      color: var(--text-primary);
    }
    .post-actions {
      display: flex;
      justify-content: space-between;
      max-width: 425px;
      color: var(--text-secondary);
    }
    .action-btn {
      display: flex;
      align-items: center;
      gap: 6px;
      background: transparent;
      color: var(--text-secondary);
      font-size: 0.85rem;
      padding: 6px 8px;
      border-radius: 9999px;
    }
    .action-btn span.material-symbols-outlined {
      font-size: 1.15rem;
    }
    .comment-btn:hover {
      color: var(--accent-color);
      background-color: rgba(29, 155, 240, 0.1);
    }
    .retweet-btn:hover {
      color: var(--success-color);
      background-color: rgba(0, 186, 124, 0.1);
    }
    .like-btn:hover {
      color: var(--danger-color);
      background-color: rgba(244, 33, 46, 0.1);
    }
    .like-btn.liked {
      color: var(--danger-color);
    }
    .like-btn.liked span.material-symbols-outlined {
      font-variation-settings: 'FILL' 1, 'wght' 300, 'GRAD' 0, 'opsz' 24;
    }

    /* Right Widget Styles */
    .user-row {
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .user-details {
      display: flex;
      align-items: center;
      cursor: pointer;
      flex-grow: 1;
    }
    .user-details:hover .display-name {
      text-decoration: underline;
    }
    .follow-btn {
      background-color: var(--text-primary);
      color: var(--bg-primary);
      font-weight: 700;
      font-size: 0.85rem;
      padding: 6px 16px;
      border-radius: 9999px;
    }
    .follow-btn:hover {
      opacity: 0.9;
    }
    .follow-btn.following {
      background-color: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
    }
    .follow-btn.following:hover {
      background-color: rgba(244, 33, 46, 0.1);
      color: var(--danger-color);
      border-color: var(--danger-color);
    }
    .follow-btn.following:hover::after {
      content: 'Unfollow';
    }
    .loading-spinner, .empty-feed {
      text-align: center;
      padding: 40px 20px;
      color: var(--text-secondary);
    }
    .empty-feed h3 {
      color: var(--text-primary);
      margin-bottom: 8px;
    }
  `]
})
export class FeedComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly currentUser = this.api.currentUser;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  // Composer State
  newPostContent = '';
  charCount = computed(() => this.newPostContent.length);
  posting = signal(false);

  // Feed State
  posts = signal<Post[]>([]);
  loadingFeed = signal(true);

  // Widgets State
  searchQuery = '';
  searchResults = signal<User[]>([]);
  suggestions = signal<User[]>([]);
  loadingSuggestions = signal(true);

  ngOnInit(): void {
    this.fetchFeed();
    this.fetchWhoToFollow();
  }

  fetchFeed(): void {
    this.loadingFeed.set(true);
    this.api.getFeed(0, 40).subscribe({
      next: (data) => {
        this.posts.set(data);
        this.loadingFeed.set(false);
      },
      error: () => {
        this.loadingFeed.set(false);
      }
    });
  }

  fetchWhoToFollow(): void {
    this.loadingSuggestions.set(true);
    this.api.searchUsers('').subscribe({
      next: (users) => {
        // Exclude current user from recommendations
        const current = this.currentUser();
        const filtered = users.filter(u => u.id !== current?.id).slice(0, 4);
        this.suggestions.set(filtered);
        this.loadingSuggestions.set(false);
      },
      error: () => {
        this.loadingSuggestions.set(false);
      }
    });
  }

  submitPost(): void {
    if (!this.newPostContent.trim()) return;
    this.posting.set(true);
    this.api.createPost(this.newPostContent).subscribe({
      next: (newPost) => {
        // Prepend new post
        this.posts.update(curr => [newPost, ...curr]);
        this.newPostContent = '';
        this.posting.set(false);
      },
      error: () => {
        this.posting.set(false);
      }
    });
  }

  deletePost(id: number): void {
    if (confirm('Are you sure you want to delete this post?')) {
      this.api.deletePost(id).subscribe({
        next: () => {
          this.posts.update(curr => curr.filter(p => p.id !== id));
        }
      });
    }
  }

  toggleLike(post: Post): void {
    const wasLiked = post.isLiked;
    // Optimistic Update
    post.isLiked = !wasLiked;
    post.likesCount += wasLiked ? -1 : 1;

    const request = wasLiked ? this.api.unlikePost(post.id) : this.api.likePost(post.id);
    request.subscribe({
      error: () => {
        // Revert on error
        post.isLiked = wasLiked;
        post.likesCount += wasLiked ? 1 : -1;
      }
    });
  }

  toggleFollow(user: User): void {
    const isFollowing = user.isFollowed;
    // Optimistic Update
    user.isFollowed = !isFollowing;

    const request = isFollowing ? this.api.unfollowUser(user.id) : this.api.followUser(user.id);
    request.subscribe({
      error: () => {
        user.isFollowed = isFollowing;
      }
    });
  }

  onSearchChange(): void {
    if (!this.searchQuery.trim()) {
      this.searchResults.set([]);
      return;
    }
    this.api.searchUsers(this.searchQuery).subscribe({
      next: (users) => {
        this.searchResults.set(users);
      }
    });
  }

  logout(): void {
    this.api.logout();
  }

  formatTime(dateStr: string): string {
    try {
      const now = new Date();
      const date = new Date(dateStr);
      const diffMs = now.getTime() - date.getTime();
      const diffMins = Math.floor(diffMs / 60000);
      const diffHours = Math.floor(diffMins / 60);

      if (diffMins < 1) return 'Just now';
      if (diffMins < 60) return `${diffMins}m`;
      if (diffHours < 24) return `${diffHours}h`;
      return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    } catch {
      return '';
    }
  }
}
