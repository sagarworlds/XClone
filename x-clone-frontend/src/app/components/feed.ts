import { Component, inject, OnInit, signal } from '@angular/core';
import { ApiService } from '../services/api.service';
import { Post } from '../models/types';
import { ComposerComponent } from './composer';
import { PostCardComponent } from './post-card';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

@Component({
  selector: 'app-feed',
  standalone: true,
  imports: [SidebarComponent, WidgetsComponent, ComposerComponent, PostCardComponent],
  template: `
    <div class="app-container">
      <app-sidebar active="home" />

      <!-- Main Feed Area -->
      <main class="main-content">
        <header class="header">
          <h2>Home</h2>
        </header>

        <app-composer [submitFn]="createPost" (posted)="onPosted($event)" />

        <!-- Posts and reposts from people you follow -->
        <div class="feed-posts">
          @if (loadingFeed()) {
            <div class="loading-spinner">Loading posts...</div>
          } @else if (posts().length === 0) {
            <div class="empty-feed">
              <h3>Welcome to X!</h3>
              <p>Follow people and start sharing what's on your mind.</p>
            </div>
          } @else {
            <!-- The same post can appear twice (original + someone's repost), so the key includes the reposter -->
            @for (post of posts(); track post.id + '-' + (post.retweetedBy?.id ?? 0)) {
              <app-post-card [post]="post" (deleted)="onDeleted($event)" />
            }
          }
        </div>
      </main>

      <app-widgets />
    </div>
  `,
  styles: [`
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

  posts = signal<Post[]>([]);
  loadingFeed = signal(true);

  readonly createPost = (content: string) => this.api.createPost(content);

  ngOnInit(): void {
    this.fetchFeed();
  }

  fetchFeed(): void {
    this.loadingFeed.set(true);
    this.api.getFeed(0, 40).subscribe({
      next: (data) => {
        this.posts.set(data);
        this.loadingFeed.set(false);
      },
      error: () => this.loadingFeed.set(false),
    });
  }

  onPosted(post: Post): void {
    this.posts.update((curr) => [post, ...curr]);
  }

  onDeleted(id: number): void {
    // Removes the post and any repost entries of it
    this.posts.update((curr) => curr.filter((p) => p.id !== id));
  }
}
