import { Injectable, OnDestroy, effect, inject, signal, untracked } from '@angular/core';
import { ApiService } from './api.service';

/** How often the unread badge asks the server whether something new came in. */
export const NOTIFICATION_POLL_MS = 30_000;

/**
 * Keeps the unread-notifications badge up to date while someone is signed in. There is no push channel, so it
 * polls (and skips the poll while the tab is in the background).
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService implements OnDestroy {
  private readonly api = inject(ApiService);

  readonly unreadCount = signal(0);

  private timer: ReturnType<typeof setInterval> | null = null;
  // Bumped when everything is marked read, so an answer to an older count request cannot bring the badge back
  private epoch = 0;

  constructor() {
    effect(() => {
      const signedIn = this.api.isAuthenticated();
      untracked(() => (signedIn ? this.start() : this.stop()));
    });
  }

  refresh(): void {
    if (!this.api.isAuthenticated()) return;

    const epoch = this.epoch;
    this.api.getUnreadNotificationCount().subscribe({
      next: ({ count }) => {
        if (epoch === this.epoch) this.unreadCount.set(count);
      },
      // A failed poll just keeps the last known number; the next one tries again
      error: () => {},
    });
  }

  /** Clears the badge right away and tells the server. */
  markAllRead(): void {
    this.epoch++;
    this.unreadCount.set(0);
    this.api.markNotificationsRead().subscribe({ error: () => this.refresh() });
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private start(): void {
    if (this.timer !== null) return;

    this.refresh();
    this.timer = setInterval(() => {
      if (!document.hidden) this.refresh();
    }, NOTIFICATION_POLL_MS);
  }

  private stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.epoch++;
    this.unreadCount.set(0);
  }
}
