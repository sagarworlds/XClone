import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { Post } from '../models/types';
import { formatTime } from '../utils/format-time';
import { mediaSrc } from '../utils/media-url';
import { PostEditorComponent } from './post-editor';
import { PostTextComponent } from './post-text';

/**
 * Identifies an entry of a timeline. The same post can appear twice (the original and someone's repost of it),
 * so the key includes who reposted it.
 */
export const postEntryKey = (post: Post): string => `${post.id}-${post.retweetedBy?.id ?? 0}`;

/**
 * A single post in a timeline or thread. It owns its own like / retweet / delete
 * interactions (optimistic, reverted on error) so pages only render lists of cards.
 */
@Component({
  selector: 'app-post-card',
  standalone: true,
  imports: [RouterLink, PostTextComponent, PostEditorComponent],
  template: `
    @let p = state();
    <article class="post-card" [class.clickable]="!focus()" [class.focus]="focus()" (click)="open($event)">
      @if (p.retweetedBy) {
        <div class="repost-banner">
          <span class="material-symbols-outlined">repeat</span>
          <span>{{ p.retweetedBy.id === currentUser()?.id ? 'You' : p.retweetedBy.displayName }} reposted</span>
        </div>
      }

      <div class="post-body">
        <img
          [src]="p.user.avatarUrl || defaultAvatar"
          alt="Avatar"
          class="avatar post-avatar"
          [routerLink]="['/profile', p.user.username]"
        />

        <div class="post-content-container">
          <div class="post-header">
            <div class="post-user-info" [routerLink]="['/profile', p.user.username]">
              <span class="post-display-name">{{ p.user.displayName }}</span>
              <span class="post-username">@{{ p.user.username }}</span>
              <span class="post-dot">·</span>
              <span class="post-time">{{ formatTime(p.createdAt) }}</span>
              @if (p.editedAt) {
                <span class="post-dot">·</span>
                <span class="post-edited" [title]="'Edited ' + editedAt(p.editedAt)">Edited</span>
              }
            </div>

            @if (isOwn()) {
              <div class="owner-actions">
                @if (!editing()) {
                  <button class="edit-post-btn" title="Edit" aria-label="Edit post" (click)="startEdit($event)">
                    <span class="material-symbols-outlined delete-icon">edit</span>
                  </button>
                }
                <button class="delete-post-btn" title="Delete" (click)="deletePost($event)">
                  <span class="material-symbols-outlined delete-icon">delete</span>
                </button>
              </div>
            }
          </div>

          @if (p.replyToUsername) {
            <div class="reply-context">
              Replying to <a [routerLink]="['/profile', p.replyToUsername]">@{{ p.replyToUsername }}</a>
            </div>
          }

          @if (editing()) {
            <app-post-editor
              [content]="p.content"
              [saving]="saving()"
              [error]="editError()"
              (submitted)="saveEdit($event)"
              (cancelled)="cancelEdit()"
            />
          } @else {
            <p class="post-text-content"><app-post-text [text]="p.content" [mentions]="p.mentions" /></p>
          }

          @if (images().length > 0) {
            <div class="media-grid" [class]="'media-grid count-' + images().length">
              @for (image of images(); track image) {
                <a class="media-item" [href]="image" target="_blank" rel="noopener noreferrer">
                  <img [src]="image" alt="Attached image" loading="lazy" />
                </a>
              }
            </div>
          }

          <div class="post-actions">
            <button class="action-btn comment-btn" title="Reply" (click)="openThread($event)">
              <span class="material-symbols-outlined">chat_bubble</span>
              <span>{{ p.repliesCount || 0 }}</span>
            </button>
            <button
              class="action-btn retweet-btn"
              [class.retweeted]="p.isRetweeted"
              [title]="p.isRetweeted ? 'Undo repost' : 'Repost'"
              (click)="toggleRetweet($event)"
            >
              <span class="material-symbols-outlined">repeat</span>
              <span>{{ p.retweetsCount || 0 }}</span>
            </button>
            <button class="action-btn like-btn" [class.liked]="p.isLiked" title="Like" (click)="toggleLike($event)">
              <span class="material-symbols-outlined" [class.filled]="p.isLiked">favorite</span>
              <span>{{ p.likesCount }}</span>
            </button>
          </div>
        </div>
      </div>
    </article>
  `,
  styles: [`
    .post-card {
      padding: 16px;
      border-bottom: 1px solid var(--border-color);
      transition: background-color 0.2s ease;
    }
    .post-card.clickable {
      cursor: pointer;
    }
    .post-card.clickable:hover {
      background-color: rgba(255, 255, 255, 0.02);
    }
    .repost-banner {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: -4px 0 8px 40px;
      color: var(--text-secondary);
      font-size: 0.85rem;
      font-weight: 700;
    }
    .repost-banner .material-symbols-outlined {
      font-size: 1rem;
    }
    .post-body {
      display: flex;
    }
    .post-avatar {
      margin-right: 12px;
      cursor: pointer;
    }
    .post-content-container {
      flex-grow: 1;
      min-width: 0;
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
    .post-username, .post-dot, .post-time, .post-edited {
      color: var(--text-secondary);
      font-size: 0.9rem;
    }
    .owner-actions {
      display: flex;
      gap: 2px;
    }
    .delete-post-btn, .edit-post-btn {
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
    .edit-post-btn:hover {
      background-color: rgba(29, 155, 240, 0.1);
      color: var(--accent-color);
    }
    .delete-icon {
      font-size: 1.2rem;
    }
    .reply-context {
      color: var(--text-secondary);
      font-size: 0.85rem;
      margin-bottom: 4px;
    }
    .reply-context a {
      color: var(--accent-color);
    }
    .reply-context a:hover {
      text-decoration: underline;
    }
    .post-text-content {
      font-size: 0.95rem;
      line-height: 1.4;
      white-space: pre-wrap;
      word-break: break-word;
      margin-bottom: 12px;
      color: var(--text-primary);
    }
    .focus .post-text-content {
      font-size: 1.25rem;
    }
    .media-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 2px;
      margin-bottom: 12px;
      border-radius: 16px;
      overflow: hidden;
      border: 1px solid var(--border-color);
    }
    .media-grid.count-1 {
      grid-template-columns: 1fr;
    }
    .media-grid.count-3 .media-item:first-child {
      grid-column: span 2;
    }
    .media-item {
      display: block;
      aspect-ratio: 16 / 9;
      background-color: var(--bg-secondary);
    }
    .media-grid.count-1 .media-item {
      aspect-ratio: auto;
      max-height: 480px;
    }
    .media-item img {
      display: block;
      width: 100%;
      height: 100%;
      object-fit: cover;
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
    .retweet-btn:hover,
    .retweet-btn.retweeted {
      color: var(--success-color);
    }
    .retweet-btn:hover {
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
  `]
})
export class PostCardComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly post = input.required<Post>();
  /** Render as the main post of a thread page: larger text, not clickable. */
  readonly focus = input(false);
  /** Emits the post id after the author deleted it. */
  readonly deleted = output<number>();
  /** Emits the post whenever its like / retweet state changes, so a parent that owns the post can stay in sync. */
  readonly changed = output<Post>();

  readonly currentUser = this.api.currentUser;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  // Local copy so optimistic updates re-render; it resets whenever the parent passes a new post.
  readonly state = linkedSignal(() => this.post());
  // Editing the text: open only for the post it was opened on (a different post handed in closes it)
  readonly editing = linkedSignal<number, boolean>({ source: () => this.post().id, computation: () => false });
  readonly saving = signal(false);
  readonly editError = signal<string | null>(null);
  readonly isOwn = computed(() => this.currentUser()?.id === this.state().userId);
  /** The post's images, as addresses to load them from (pictures that are not ours are left out). */
  readonly images = computed(() =>
    (this.state().mediaUrls ?? []).map(mediaSrc).filter((src): src is string => src !== null),
  );

  open(event: MouseEvent): void {
    if (this.focus()) return;
    // Buttons, links and the avatar/name (which link to the profile) handle their own clicks.
    if ((event.target as HTMLElement).closest('button, a, .post-avatar, .post-user-info')) return;
    this.router.navigate(['/post', this.state().id]);
  }

  openThread(event: Event): void {
    event.stopPropagation();
    if (this.focus()) return;
    this.router.navigate(['/post', this.state().id]);
  }

  toggleLike(event: Event): void {
    event.stopPropagation();
    const before = this.state();
    this.update({
      ...before,
      isLiked: !before.isLiked,
      likesCount: before.likesCount + (before.isLiked ? -1 : 1),
    });
    this.api.likePost(before.id).subscribe({ error: () => this.update(before) });
  }

  toggleRetweet(event: Event): void {
    event.stopPropagation();
    const before = this.state();
    this.update({
      ...before,
      isRetweeted: !before.isRetweeted,
      retweetsCount: before.retweetsCount + (before.isRetweeted ? -1 : 1),
    });
    this.api.toggleRetweet(before.id).subscribe({ error: () => this.update(before) });
  }

  private update(next: Post): void {
    this.state.set(next);
    this.changed.emit(next);
  }

  startEdit(event: Event): void {
    event.stopPropagation();
    this.editError.set(null);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.editError.set(null);
  }

  saveEdit(content: string): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.editError.set(null);
    this.api.updatePost(this.state().id, content).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.editing.set(false);
        // The answer is the post itself; in a timeline the entry still says who reposted it
        this.update({ ...updated, retweetedBy: this.state().retweetedBy });
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.editError.set(err.error?.message ?? 'Could not save your changes. Please try again.');
      },
    });
  }

  deletePost(event: Event): void {
    event.stopPropagation();
    if (!confirm('Are you sure you want to delete this post?')) return;
    const id = this.state().id;
    this.api.deletePost(id).subscribe({
      next: () => this.deleted.emit(id),
      error: () => alert('Could not delete the post. Please try again.'),
    });
  }

  readonly formatTime = formatTime;
  readonly editedAt = (iso: string): string => new Date(iso).toLocaleString();
}
