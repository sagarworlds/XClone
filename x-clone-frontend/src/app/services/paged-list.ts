import { computed, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { Page } from '../models/types';

/** What the "Load more" button needs to know about a list. */
export interface LoadMoreSource {
  readonly hasMore: () => boolean;
  readonly loadingMore: () => boolean;
  readonly failed: () => boolean;
  loadMore(): void;
}

/**
 * A list that is fetched one page at a time from an endpoint that pages with cursors: every answer says where the
 * next page starts (`nextCursor`), or that there is none.
 *
 * A cursor is a position ("right after this entry"), not a count, so posting, deleting or un-reposting while the page
 * is open cannot make the next page repeat or skip anything: the list only has to add and remove entries locally.
 * (Entries the server does return twice are still dropped by their key, as a safety net.)
 */
export class PagedList<T> {
  private readonly loaded = signal<T[]>([]);
  /** Entries added locally at the end while pages before them are still unloaded, so they stay after those pages. */
  private readonly tail = signal<T[]>([]);

  readonly items = computed(() => [...this.loaded(), ...this.tail()]);
  /** The first page is loading. */
  readonly loading = signal(false);
  /** A following page is loading. */
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);
  /** The last request failed. */
  readonly failed = signal(false);

  private nextCursor: string | null = null;
  private firstPageLoaded = false;
  private request: Subscription | null = null;

  private readonly fetchPage: (cursor: string | null, take: number) => Observable<Page<T>>;
  private readonly keyOf: (item: T) => string;
  readonly pageSize: number;

  constructor(fetchPage: (cursor: string | null, take: number) => Observable<Page<T>>, keyOf: (item: T) => string, pageSize = 20) {
    this.fetchPage = fetchPage;
    this.keyOf = keyOf;
    this.pageSize = pageSize;
  }

  /** Forgets everything and stops any request in flight (a different profile or thread is about to be shown). */
  reset(): void {
    this.request?.unsubscribe();
    this.request = null;
    this.loaded.set([]);
    this.tail.set([]);
    this.nextCursor = null;
    this.firstPageLoaded = false;
    this.loading.set(false);
    this.loadingMore.set(false);
    this.hasMore.set(false);
    this.failed.set(false);
  }

  loadFirst(): void {
    this.reset();
    this.loading.set(true);
    this.request = this.fetchPage(null, this.pageSize).subscribe({
      next: (page) => {
        this.firstPageLoaded = true;
        this.accept(page);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** Loads the first page unless it is already there or on its way (used by tabs that load lazily). */
  ensureLoaded(): void {
    if (!this.firstPageLoaded && !this.loading()) {
      this.loadFirst();
    }
  }

  loadMore(): void {
    if (!this.firstPageLoaded || !this.hasMore() || this.loadingMore()) return;

    this.loadingMore.set(true);
    this.failed.set(false);
    this.request = this.fetchPage(this.nextCursor, this.pageSize).subscribe({
      next: (page) => {
        this.accept(page);
        this.loadingMore.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loadingMore.set(false);
      },
    });
  }

  /** Something new at the top, e.g. a post the user just wrote. */
  addFirst(item: T): void {
    this.loaded.update((current) => [item, ...current]);
  }

  /** Something new at the end, e.g. a reply in an oldest-first thread. */
  addLast(item: T): void {
    if (this.hasMore()) {
      // The pages before it are not loaded yet: keep it after them, and let the page that contains it take over
      this.tail.update((current) => [...current, item]);
    } else {
      this.loaded.update((current) => [...current, item]);
    }
  }

  remove(matches: (item: T) => boolean): void {
    this.loaded.update((current) => current.filter((item) => !matches(item)));
    this.tail.update((current) => current.filter((item) => !matches(item)));
  }

  private accept(page: Page<T>): void {
    const keys = new Set(this.loaded().map(this.keyOf));
    const fresh = page.items.filter((item) => {
      const key = this.keyOf(item);
      if (keys.has(key)) return false;
      keys.add(key);
      return true;
    });

    const returned = new Set(page.items.map(this.keyOf));
    this.loaded.update((current) => [...current, ...fresh]);
    this.tail.update((current) => current.filter((item) => !returned.has(this.keyOf(item))));

    this.nextCursor = page.nextCursor;
    this.hasMore.set(page.nextCursor !== null);
  }
}
