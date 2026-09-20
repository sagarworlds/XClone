import { Component, inject, OnInit, signal } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { Post } from '../models/types';
import { ComposerComponent } from './composer';
import { LoadMoreComponent } from './load-more';
import { PostCardComponent } from './post-card';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

/** A post with its conversation: the post itself, a reply box, and its direct replies. */
@Component({
  selector: 'app-post-detail',
  standalone: true,
  imports: [RouterLink, SidebarComponent, WidgetsComponent, ComposerComponent, PostCardComponent, LoadMoreComponent],
  template: `
    <div class="app-container">
      <app-sidebar />

      <main class="main-content">
        <header class="header detail-header">
          <button class="back-btn" title="Back" (click)="goBack()">
            <span class="material-symbols-outlined">arrow_back</span>
          </button>
          <h2>Post</h2>
        </header>

        @if (loading()) {
          <div class="state-message">Loading post...</div>
        } @else if (!post()) {
          <div class="state-message">
            <h3>Post not found</h3>
            <p>This post may have been deleted.</p>
            <a routerLink="/home" class="back-link">Go Home</a>
          </div>
        } @else {
          @if (post()!.parentPostId) {
            <a class="parent-link" [routerLink]="['/post', post()!.parentPostId]">
              <span class="material-symbols-outlined">arrow_upward</span> View parent post
            </a>
          }

          <app-post-card [post]="post()!" [focus]="true" (changed)="post.set($event)" (deleted)="onPostDeleted()" />

          <app-composer
            placeholder="Post your reply"
            buttonLabel="Reply"
            [rows]="2"
            [submitFn]="createReply"
            (posted)="onReplyPosted($event)"
          />

          <div class="replies">
            @if (replies.loading()) {
              <div class="state-message">Loading replies...</div>
            } @else {
              @for (reply of replies.items(); track reply.id) {
                <app-post-card [post]="reply" (deleted)="onReplyDeleted($event)" />
              } @empty {
                <div class="state-message">No replies yet. Be the first to reply.</div>
              }
              <app-load-more [list]="replies" />
            }
          </div>
        }
      </main>

      <app-widgets />
    </div>
  `,
  styles: [`
    .detail-header {
      display: flex;
      align-items: center;
      gap: 24px;
    }
    .back-btn {
      background: transparent;
      color: var(--text-primary);
      border-radius: 50%;
      padding: 6px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .back-btn:hover {
      background-color: rgba(255, 255, 255, 0.1);
    }
    .parent-link {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 12px 16px 0;
      color: var(--accent-color);
      font-size: 0.9rem;
    }
    .parent-link:hover {
      text-decoration: underline;
    }
    .parent-link .material-symbols-outlined {
      font-size: 1.1rem;
    }
    .state-message {
      text-align: center;
      padding: 40px 20px;
      color: var(--text-secondary);
    }
    .state-message h3 {
      color: var(--text-primary);
      margin-bottom: 8px;
    }
    .back-link {
      color: var(--accent-color);
    }
  `]
})
export class PostDetailComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  post = signal<Post | null>(null);
  readonly replies = new PagedList<Post>((cursor, take) => this.api.getReplies(this.post()!.id, cursor, take), (p) => String(p.id));
  loading = signal(true);

  readonly createReply = (content: string, mediaUrls: string[]) => this.api.createReply(this.post()!.id, content, mediaUrls);

  ngOnInit(): void {
    // The same component instance is reused when navigating from a post to one of its replies
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (Number.isInteger(id) && id > 0) {
        this.load(id);
      } else {
        this.post.set(null);
        this.loading.set(false);
      }
    });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.replies.reset();
    this.api.getPost(id).subscribe({
      next: (post) => {
        this.post.set(post);
        this.loading.set(false);
        this.replies.loadFirst();
      },
      error: () => {
        this.post.set(null);
        this.loading.set(false);
      },
    });
  }

  onReplyPosted(reply: Post): void {
    this.replies.addLast(reply);
    this.post.update((p) => (p ? { ...p, repliesCount: p.repliesCount + 1 } : p));
  }

  onReplyDeleted(id: number): void {
    this.replies.remove((r) => r.id === id);
    this.post.update((p) => (p ? { ...p, repliesCount: Math.max(0, p.repliesCount - 1) } : p));
  }

  onPostDeleted(): void {
    const parentId = this.post()?.parentPostId;
    this.router.navigate(parentId ? ['/post', parentId] : ['/home']);
  }

  goBack(): void {
    if (window.history.length > 1) {
      this.location.back();
    } else {
      this.router.navigate(['/home']);
    }
  }
}
