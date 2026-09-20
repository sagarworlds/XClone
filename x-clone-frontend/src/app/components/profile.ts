import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { Post, User } from '../models/types';
import { LoadMoreComponent } from './load-more';
import { PostCardComponent, postEntryKey } from './post-card';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SidebarComponent, WidgetsComponent, PostCardComponent, LoadMoreComponent],
  template: `
    <div class="app-container">
      <app-sidebar [active]="isOwnProfile() ? 'profile' : null" />

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
              <span class="tweet-count">{{ postsLabel() }}</span>
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
                <a class="follow-item" [routerLink]="['/profile', profile()?.username, 'following']">
                  <strong class="count-value">{{ profile()?.followingCount || 0 }}</strong> Following
                </a>
                <a class="follow-item" [routerLink]="['/profile', profile()?.username, 'followers']">
                  <strong class="count-value">{{ profile()?.followersCount || 0 }}</strong> Followers
                </a>
              </div>
            </div>
          </div>

          <!-- Profile Tabs -->
          <div class="profile-tabs">
            <div class="tab" [class.active]="activeTab() === 'posts'" (click)="selectTab('posts')">Posts</div>
            <div class="tab" [class.active]="activeTab() === 'replies'" (click)="selectTab('replies')">Replies</div>
          </div>

          <div class="feed-posts">
            @if (activeTab() === 'posts') {
              @if (postList.loading()) {
                <div class="loading-spinner">Loading posts...</div>
              } @else if (postList.items().length === 0) {
                <div class="empty-feed">
                  <h3>No posts yet</h3>
                  <p>@{{ profile()?.username }} hasn't posted anything yet.</p>
                </div>
              } @else {
                @for (post of postList.items(); track entryKey(post)) {
                  <app-post-card [post]="post" (deleted)="onPostDeleted($event)" (changed)="onPostChanged($event)" />
                }
                <app-load-more [list]="postList" />
              }
            } @else {
              @if (replyList.loading()) {
                <div class="loading-spinner">Loading replies...</div>
              } @else if (replyList.items().length === 0) {
                <div class="empty-feed">
                  <h3>No replies yet</h3>
                  <p>@{{ profile()?.username }} hasn't replied to anyone yet.</p>
                </div>
              } @else {
                @for (reply of replyList.items(); track reply.id) {
                  <app-post-card [post]="reply" (deleted)="onPostDeleted($event)" />
                }
                <app-load-more [list]="replyList" />
              }
            }
          </div>
        }
      </main>

      <app-widgets />
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
      color: inherit;
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
    /* Right Widget Styles */
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
  readonly postList = new PagedList<Post>((cursor, take) => this.api.getUserPosts(this.profile()!.id, cursor, take), postEntryKey);
  readonly replyList = new PagedList<Post>((cursor, take) => this.api.getUserReplies(this.profile()!.id, cursor, take), (p) => String(p.id));
  readonly entryKey = postEntryKey;
  isOwnProfile = computed(() => this.profile()?.id === this.currentUser()?.id);
  postsLabel = computed(() => {
    const count = this.profile()?.postsCount ?? 0;
    return `${count} ${count === 1 ? 'post' : 'posts'}`;
  });

  loadingProfile = signal(true);

  // Edit Modal State
  showEditModal = signal(false);
  savingProfile = signal(false);
  saveError = signal<string | null>(null);
  editForm = {
    displayName: '',
    bio: '',
    avatarUrl: ''
  };

  // Tabs
  activeTab = signal<'posts' | 'replies'>('posts');

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
    this.activeTab.set('posts');
    this.postList.reset();
    this.replyList.reset();
    this.api.getUserProfileByUsername(username).subscribe({
      next: (user) => {
        this.profile.set(user);
        this.loadingProfile.set(false);
        this.postList.loadFirst();
      },
      error: () => {
        this.profile.set(null);
        this.loadingProfile.set(false);
      }
    });
  }

  selectTab(tab: 'posts' | 'replies'): void {
    this.activeTab.set(tab);
    if (tab === 'replies' && this.profile()) {
      this.replyList.ensureLoaded();
    }
  }

  onPostDeleted(id: number): void {
    // Removes the post and any repost entries of it (deleting a post also deletes its replies)
    this.postList.remove(p => p.id === id);
    this.replyList.remove(p => p.id === id);
    this.refreshPostsCount();
  }

  onPostChanged(post: Post): void {
    // Undoing your own repost takes its entry out of the list
    if (post.retweetedBy?.id === this.currentUser()?.id && !post.isRetweeted) {
      this.postList.remove(p => postEntryKey(p) === postEntryKey(post));
      // Exactly one entry of yours went away. (Asking the server would race with the undo that is still on its way.)
      this.profile.update(p => (p ? { ...p, postsCount: Math.max(0, p.postsCount - 1) } : p));
    }
  }

  /** Deleting a post can take more than one entry of yours with it (a repost of your own post), so ask the server for the new total. */
  private refreshPostsCount(): void {
    const prof = this.profile();
    if (!prof || !this.isOwnProfile()) return;

    this.api.getUserProfileByUsername(prof.username).subscribe({
      next: (fresh) => this.profile.update(p => (p && p.id === fresh.id ? { ...p, postsCount: fresh.postsCount } : p)),
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

  formatJoinedDate(dateStr?: string): string {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString(undefined, { year: 'numeric', month: 'long' });
    } catch {
      return '';
    }
  }
}
