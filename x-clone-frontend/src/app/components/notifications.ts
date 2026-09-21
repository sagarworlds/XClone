import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { tap } from 'rxjs';
import { ApiService } from '../services/api.service';
import { NotificationsService } from '../services/notifications.service';
import { PagedList } from '../services/paged-list';
import { AppNotification } from '../models/types';
import { formatTime } from '../utils/format-time';
import { LoadMoreComponent } from './load-more';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

/** Who replied to, reposted or mentioned you, newest first. Opening the page marks everything as read. */
@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [RouterLink, SidebarComponent, WidgetsComponent, LoadMoreComponent],
  template: `
    <div class="app-container">
      <app-sidebar active="notifications" />

      <main class="main-content">
        <header class="header">
          <h2>Notifications</h2>
        </header>

        @if (list.loading()) {
          <div class="state-message">Loading notifications...</div>
        } @else if (list.items().length === 0) {
          <div class="state-message">
            <h3>{{ list.failed() ? "Couldn't load notifications" : 'Nothing to see yet' }}</h3>
            <p>{{ list.failed() ? 'Check your connection and reload the page.' : "When someone replies to or reposts your posts, or mentions you, you'll see it here." }}</p>
          </div>
        } @else {
          @for (n of list.items(); track n.id) {
            <a class="notification" [class.unread]="!n.isRead" [routerLink]="['/post', n.postId]">
              <span class="material-symbols-outlined type-icon" [class.repost]="n.type === 'repost'" [class.mention]="n.type === 'mention'">
                {{ iconOf(n.type) }}
              </span>
              <div class="notification-body">
                <img [src]="n.actor.avatarUrl || defaultAvatar" alt="" class="avatar actor-avatar" />
                <p class="summary">
                  <strong>{{ n.actor.displayName || n.actor.username }}</strong>
                  {{ summaryOf(n.type) }}
                  <span class="time">· {{ formatTime(n.createdAt) }}</span>
                </p>
                <p class="excerpt">{{ n.postContent }}</p>
              </div>
            </a>
          }
          <app-load-more [list]="list" />
        }
      </main>

      <app-widgets />
    </div>
  `,
  styles: [`
    .state-message {
      text-align: center;
      padding: 40px 20px;
      color: var(--text-secondary);
    }
    .state-message h3 {
      color: var(--text-primary);
      margin-bottom: 8px;
    }
    .notification {
      display: flex;
      gap: 12px;
      padding: 16px;
      border-bottom: 1px solid var(--border-color);
      color: var(--text-primary);
    }
    .notification:hover {
      background-color: rgba(255, 255, 255, 0.03);
      text-decoration: none;
    }
    .notification.unread {
      background-color: rgba(29, 155, 240, 0.08);
      box-shadow: inset 3px 0 0 var(--accent-color);
    }
    .type-icon {
      font-size: 1.6rem;
      color: var(--accent-color);
    }
    .type-icon.repost {
      color: var(--success-color);
    }
    .type-icon.mention {
      color: var(--warning-color, #ffd400);
    }
    .notification-body {
      min-width: 0;
      flex-grow: 1;
    }
    .actor-avatar {
      width: 32px;
      height: 32px;
      margin-bottom: 8px;
    }
    .summary {
      overflow-wrap: anywhere;
    }
    .time {
      color: var(--text-secondary);
    }
    .excerpt {
      margin-top: 4px;
      color: var(--text-secondary);
      overflow-wrap: anywhere;
      white-space: pre-wrap;
    }
  `]
})
export class NotificationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly notifications = inject(NotificationsService);

  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';
  readonly formatTime = formatTime;

  iconOf(type: AppNotification['type']): string {
    return type === 'repost' ? 'repeat' : type === 'mention' ? 'alternate_email' : 'chat_bubble';
  }

  summaryOf(type: AppNotification['type']): string {
    return type === 'repost' ? 'reposted your post' : type === 'mention' ? 'mentioned you in a post' : 'replied to your post';
  }

  readonly list = new PagedList<AppNotification>(
    (cursor, take) =>
      this.api.getNotifications(cursor, take).pipe(
        // The page keeps showing which ones were new; the server and the badge learn they have been seen
        tap((page) => {
          if (cursor === null && page.items.some((n) => !n.isRead)) this.notifications.markAllRead();
        }),
      ),
    (n) => String(n.id),
  );

  ngOnInit(): void {
    this.list.loadFirst();
  }
}
