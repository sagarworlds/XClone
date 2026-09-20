import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { Post, User } from '../models/types';

@Component({
  selector: 'app-profile',
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
            <a routerLink="/home" class="nav-item">
              <span class="material-symbols-outlined">home</span>
              <span>Home</span>
            </a>
            @if (currentUser()) {
              <a [routerLink]="['/profile', currentUser()?.username]" class="nav-item" [class.active]="isOwnProfile()">
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

      <!-- Main Profile Area -->
      <main class="main-content">
        @if (loadingProfile()) {
          <div class="loading-spinner">Loading profile...</div>
        } @else if (!profile()) {
          <div class="empty-feed">
            <h3>User not found</h3>
            <p>The profile you are looking for does not exist.</p>
            <a routerLink="/home" class="back-link">Go Home</a>
          </div>
        } @else {
          <!-- Header with display name and tweet count -->
          <header class="header profile-header">
            <button class="back-btn" routerLink="/home">
              <span class="material-symbols-outlined">arrow_back</span>
            </button>
            <div class="header-titles">
              <h2>{{ profile()?.displayName }}</h2>
              <span class="tweet-count">{{ posts().length }} post(s)</span>
            </div>
          </header>

          <!-- Profile Cover Banner & Info -->
          <div class="cover-banner"></div>
          
          <div class="profile-details-container">
            <div class="avatar-and-actions">
              <img [src]="profile()?.avatarUrl || defaultAvatar" alt="Avatar" class="profile-avatar-large" />
              
              <div class="profile-actions">
                @if (isOwnProfile()) {
                  <button (click)="openEditModal()" class="edit-profile-btn">Edit profile</button>
                } @else {
                  <button 
                    (click)="toggleFollow()" 
                    class="profile-follow-btn" 
                    [class.following]="profile()?.isFollowed"
                  >
                    {{ profile()?.isFollowed ? 'Following' : 'Follow' }}
                  </button>
                }
              </div>
            </div>

            <div class="profile-info-section">
              <h2 class="profile-display-name">{{ profile()?.displayName }}</h2>
              <span class="profile-username">@{{ profile()?.username }}</span>
              
              @if (profile()?.bio) {
                <p class="profile-bio">{{ profile()?.bio }}</p>
              }

              <div class="profile-meta">
                <span class="material-symbols-outlined meta-icon">calendar_month</span>
                <span class="meta-text">Joined {{ formatJoinedDate(profile()?.createdAt) }}</span>
              </div>

              <div class="follow-counts">
                <span class="follow-item">
                  <strong class="count-value">{{ profile()?.followingCount || 0 }}</strong> Following
                </span>
                <span class="follow-item">
                  <strong class="count-value">{{ profile()?.followersCount || 0 }}</strong> Followers
                </span>
              </div>
            </div>
          </div>

          <!-- User Tweets Tabs -->
          <div class="profile-tabs">
            <div class="tab active">Posts</div>
          </div>

          <!-- User Tweets List -->
          <div class="feed-posts">
            @if (loadingPosts()) {
              <div class="loading-spinner">Loading posts...</div>
            } @else if (posts().length === 0) {
              <div class="empty-feed">
                <h3>No posts yet</h3>
                <p>@{{ profile()?.username }} hasn't posted anything yet.</p>
              </div>
            } @else {
              @for (post of posts(); track post.id) {
                <div class="post-card">
                  <img 
                    [src]="profile()?.avatarUrl || defaultAvatar" 
                    alt="Avatar" 
                    class="avatar post-avatar"
                  />
                  
                  <div class="post-content-container">
                    <div class="post-header">
                      <div class="post-user-info">
                        <span class="post-display-name">{{ profile()?.displayName }}</span>
                        <span class="post-username">@{{ profile()?.username }}</span>
                        <span class="post-dot">·</span>
                        <span class="post-time">{{ formatTime(post.createdAt) }}</span>
                      </div>
                      
                      @if (isOwnProfile()) {
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
                          favorite
                        </span>
                        <span>{{ post.likesCount }}</span>
                      </button>
                    </div>
                  </div>
                </div>
              }
            }
          </div>
        }
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
      </aside>
    </div>

    <!-- Edit Profile Modal Overlay -->
    @if (showEditModal()) {
      <div class="modal-backdrop">
        <div class="modal-card">
          <div class="modal-header">
            <button class="close-btn" (click)="closeEditModal()">
              <span class="material-symbols-outlined">close</span>
            </button>
            <h3>Edit Profile</h3>
            <button class="save-btn" (click)="saveProfile()" [disabled]="savingProfile()">
              Save
            </button>
          </div>

          <div class="modal-body">
            <div class="form-group-modal">
              <label>Display Name</label>
              <input type="text" [(ngModel)]="editForm.displayName" required />
            </div>

            <div class="form-group-modal">
              <label>Bio</label>
              <textarea [(ngModel)]="editForm.bio" rows="4"></textarea>
            </div>

            <div class="form-group-modal">
              <label>Avatar Image URL</label>
              <input type="text" [(ngModel)]="editForm.avatarUrl" placeholder="https://..." />
            </div>

            @if (saveError()) {
              <p class="modal-error">{{ saveError() }}</p>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .profile-header {
      display: flex;
      align-items: center;
      gap: 20px;
    }
    .back-btn {
      background: transparent;
      color: var(--text-primary);
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .back-btn:hover {
      background-color: rgba(231, 233, 234, 0.1);
    }
    .header-titles h2 {
      line-height: 1.2;
    }
    .tweet-count {
      font-size: 0.8rem;
      color: var(--text-secondary);
    }
    .cover-banner {
      height: 200px;
      background-color: #333639;
      border-bottom: 1px solid var(--border-color);
    }
    .profile-details-container {
      padding: 0 16px 16px 16px;
      border-bottom: 1px solid var(--border-color);
      position: relative;
    }
    .avatar-and-actions {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      margin-top: -70px;
      margin-bottom: 16px;
    }
    .profile-avatar-large {
      width: 140px;
      height: 140px;
      border-radius: 50%;
      border: 4px solid var(--bg-primary);
      object-fit: cover;
      background-color: var(--border-color);
    }
    .profile-actions {
      margin-bottom: 10px;
    }
    .edit-profile-btn, .profile-follow-btn {
      background: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      border-radius: 9999px;
      padding: 8px 16px;
      font-weight: 700;
      font-size: 0.95rem;
    }
    .edit-profile-btn:hover {
      background-color: rgba(231, 233, 234, 0.1);
    }
    .profile-follow-btn {
      background-color: var(--text-primary);
      color: var(--bg-primary);
    }
    .profile-follow-btn.following {
      background-color: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
    }
    .profile-follow-btn.following:hover {
      background-color: rgba(244, 33, 46, 0.1);
      color: var(--danger-color);
      border-color: var(--danger-color);
    }
    .profile-info-section {
      margin-top: 8px;
    }
    .profile-display-name {
      font-size: 1.5rem;
      font-weight: 800;
      line-height: 1.1;
    }
    .profile-username {
      color: var(--text-secondary);
      font-size: 0.95rem;
    }
    .profile-bio {
      margin-top: 12px;
      font-size: 0.95rem;
      line-height: 1.4;
      white-space: pre-wrap;
    }
    .profile-meta {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-top: 12px;
      color: var(--text-secondary);
      font-size: 0.9rem;
    }
    .meta-icon {
      font-size: 1.1rem;
    }
    .follow-counts {
      display: flex;
      gap: 20px;
      margin-top: 12px;
      font-size: 0.9rem;
      color: var(--text-secondary);
    }
    .follow-item {
      cursor: pointer;
    }
    .follow-item:hover {
      text-decoration: underline;
    }
    .count-value {
      color: var(--text-primary);
      font-weight: 700;
    }

    .profile-tabs {
      display: flex;
      border-bottom: 1px solid var(--border-color);
    }
    .profile-tabs .tab {
      flex: 1;
      text-align: center;
      padding: 16px;
      font-weight: 700;
      font-size: 0.95rem;
      color: var(--text-secondary);
      border-bottom: 4px solid transparent;
      cursor: pointer;
    }
    .profile-tabs .tab.active {
      color: var(--text-primary);
      border-color: var(--accent-color);
    }

    /* Modal Styling */
    .modal-backdrop {
      position: fixed;
      top: 0;
      left: 0;
      width: 100vw;
      height: 100vh;
      background-color: rgba(91, 112, 131, 0.4);
      display: flex;
      justify-content: center;
      align-items: center;
      z-index: 100;
    }
    .modal-card {
      width: 100%;
      max-width: 600px;
      background-color: var(--bg-primary);
      border-radius: 16px;
      border: 1px solid var(--border-color);
      display: flex;
      flex-direction: column;
      max-height: 90vh;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.6);
    }
    .modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      border-bottom: 1px solid var(--border-color);
    }
    .modal-header h3 {
      font-weight: 700;
      font-size: 1.25rem;
      flex-grow: 1;
      margin-left: 20px;
    }
    .close-btn {
      background: transparent;
      color: var(--text-primary);
      border-radius: 50%;
      padding: 4px;
    }
    .close-btn:hover {
      background-color: rgba(231, 233, 234, 0.1);
    }
    .save-btn {
      background-color: var(--text-primary);
      color: var(--bg-primary);
      font-weight: 700;
      padding: 6px 20px;
      border-radius: 9999px;
      font-size: 0.9rem;
    }
    .save-btn:hover {
      opacity: 0.9;
    }
    .modal-error {
      color: #f4212e;
      font-size: 14px;
      margin: 0;
    }
    .modal-body {
      padding: 24px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .form-group-modal {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .form-group-modal label {
      font-size: 0.85rem;
      color: var(--text-secondary);
    }
    .form-group-modal input, .form-group-modal textarea {
      padding: 12px;
      font-size: 1rem;
      border-radius: 6px;
      background: transparent;
      border: 1px solid var(--border-color);
      color: var(--text-primary);
    }
    .form-group-modal input:focus, .form-group-modal textarea:focus {
      border-color: var(--accent-color);
    }
    .back-link {
      display: inline-block;
      margin-top: 12px;
      background-color: var(--accent-color);
      color: #fff;
      padding: 8px 20px;
      border-radius: 9999px;
      font-weight: 700;
    }
    .back-link:hover {
      text-decoration: none;
      background-color: var(--accent-hover);
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
    .loading-spinner, .empty-feed {
      text-align: center;
      padding: 40px 20px;
      color: var(--text-secondary);
    }
  `]
})
export class ProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);

  readonly currentUser = this.api.currentUser;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  // Profile State
  profile = signal<User | null>(null);
  posts = signal<Post[]>([]);
  isOwnProfile = computed(() => this.profile()?.id === this.currentUser()?.id);

  loadingProfile = signal(true);
  loadingPosts = signal(true);

  // Edit Modal State
  showEditModal = signal(false);
  savingProfile = signal(false);
  saveError = signal<string | null>(null);
  editForm = {
    displayName: '',
    bio: '',
    avatarUrl: ''
  };

  // Widgets State
  searchQuery = '';
  searchResults = signal<User[]>([]);

  ngOnInit(): void {
    // React to username param changes
    this.route.paramMap.subscribe(params => {
      const username = params.get('username');
      if (username) {
        this.fetchProfile(username);
      }
    });
  }

  fetchProfile(username: string): void {
    this.loadingProfile.set(true);
    this.api.getUserProfileByUsername(username).subscribe({
      next: (user) => {
        this.profile.set(user);
        this.loadingProfile.set(false);
        this.fetchUserPosts(user.id);
      },
      error: () => {
        this.profile.set(null);
        this.loadingProfile.set(false);
      }
    });
  }

  fetchUserPosts(userId: number): void {
    this.loadingPosts.set(true);
    this.api.getUserPosts(userId, 0, 40).subscribe({
      next: (data) => {
        this.posts.set(data);
        this.loadingPosts.set(false);
      },
      error: () => {
        this.loadingPosts.set(false);
      }
    });
  }

  toggleFollow(): void {
    const prof = this.profile();
    if (!prof) return;

    const isFollowing = prof.isFollowed;
    // Optimistic Update
    prof.isFollowed = !isFollowing;
    prof.followersCount += isFollowing ? -1 : 1;
    this.profile.set({ ...prof });

    const request = isFollowing ? this.api.unfollowUser(prof.id) : this.api.followUser(prof.id);
    request.subscribe({
      error: () => {
        // Revert on error
        prof.isFollowed = isFollowing;
        prof.followersCount += isFollowing ? 1 : -1;
        this.profile.set({ ...prof });
      }
    });
  }

  toggleLike(post: Post): void {
    const wasLiked = post.isLiked;
    // Optimistic Update
    post.isLiked = !wasLiked;
    post.likesCount += wasLiked ? -1 : 1;

    const request = wasLiked ? this.api.unlikePost(post.id) : this.api.likePost(post.id);
    request.subscribe({
      error: () => {
        post.isLiked = wasLiked;
        post.likesCount += wasLiked ? 1 : -1;
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

  // Edit Modal Operations
  openEditModal(): void {
    const prof = this.profile();
    if (prof) {
      this.editForm = {
        displayName: prof.displayName || '',
        bio: prof.bio || '',
        avatarUrl: prof.avatarUrl || ''
      };
      this.saveError.set(null);
      this.showEditModal.set(true);
    }
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
  }

  saveProfile(): void {
    if (!this.editForm.displayName.trim()) return;
    this.savingProfile.set(true);
    this.saveError.set(null);

    this.api.updateProfile(this.editForm).subscribe({
      next: (updatedUser) => {
        this.profile.set(updatedUser);
        this.savingProfile.set(false);
        this.closeEditModal();
      },
      error: (err) => {
        this.savingProfile.set(false);
        this.saveError.set(
          err.error?.errors?.AvatarUrl?.[0] || err.error?.message || 'Could not save your profile. Please try again.'
        );
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

  formatJoinedDate(dateStr?: string): string {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString(undefined, { year: 'numeric', month: 'long' });
    } catch {
      return '';
    }
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
