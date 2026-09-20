import { Component, inject, OnInit } from '@angular/core';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { Post } from '../models/types';
import { ComposerComponent } from './composer';
import { LoadMoreComponent } from './load-more';
import { PostCardComponent, postEntryKey } from './post-card';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

@Component({
  selector: 'app-feed',
  standalone: true,
  imports: [SidebarComponent, WidgetsComponent, ComposerComponent, PostCardComponent, LoadMoreComponent],
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
          @if (feed.loading()) {
            <div class="loading-spinner">Loading posts...</div>
          } @else if (feed.items().length === 0) {
            <div class="empty-feed">
              <h3>Welcome to X!</h3>
              <p>Follow people and start sharing what's on your mind.</p>
            </div>
          } @else {
            @for (post of feed.items(); track entryKey(post)) {
              <app-post-card [post]="post" (deleted)="onDeleted($event)" (changed)="onChanged($event)" />
            }
            <app-load-more [list]="feed" />
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

  readonly feed = new PagedList<Post>((cursor, take) => this.api.getFeed(cursor, take), postEntryKey);
  readonly entryKey = postEntryKey;

  readonly createPost = (content: string, mediaUrls: string[]) => this.api.createPost(content, mediaUrls);

  ngOnInit(): void {
    this.feed.loadFirst();
  }

  onPosted(post: Post): void {
    this.feed.addFirst(post);
  }

  onDeleted(id: number): void {
    // Removes the post and any repost entries of it
    this.feed.remove((p) => p.id === id);
  }

  onChanged(post: Post): void {
    // Undoing your own repost takes its entry out of the timeline
    if (post.retweetedBy?.id === this.api.currentUser()?.id && !post.isRetweeted) {
      this.feed.remove((p) => postEntryKey(p) === postEntryKey(post));
    }
  }
}
