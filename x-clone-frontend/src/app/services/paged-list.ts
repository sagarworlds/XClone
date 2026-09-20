import { computed, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

/** What the "Load more" button needs to know about a list. */
export interface LoadMoreSource {
  readonly hasMore: () => boolean;
  readonly loadingMore: () => boolean;
  readonly failed: () => boolean;
  loadMore(): void;
}

/**
 * A list that is fetched one page at a time from an endpoint that pages with skip/take.
 *
 * The server's list keeps changing while the page is open (the user posts, deletes, un-reposts, others post), so
 * the next page's `skip` is not just `items().length`. `offset` counts the server entries already covered and is
 * kept in step by addFirst / addLast / remove; an entry the server still returns twice is dropped by its key.
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

  private offset = 0;
  private firstPageLoaded = false;
  private request: Subscription | null = null;

  private readonly fetchPage: (skip: number, take: number) => Observable<T[]>;
  private readonly keyOf: (item: T) => string;
  readonly pageSize: number;

  constructor(fetchPage: (skip: number, take: number) => Observable<T[]>, keyOf: (item: T) => string, pageSize = 20) {
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
    this.offset = 0;
    this.firstPageLoaded = false;
    this.loading.set(false);
    this.loadingMore.set(false);
    this.hasMore.set(false);
    this.failed.set(false);
  }

  loadFirst(): void {
    this.reset();
    this.loading.set(true);
    this.request = this.fetchPage(0, this.pageSize).subscribe({
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
    this.request = this.fetchPage(this.offset, this.pageSize).subscribe({
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

  /** Something new at the top of the server's list, e.g. a post the user just wrote. */
  addFirst(item: T): void {
    this.loaded.update((current) => [item, ...current]);
    this.offset++;
  }

  /** Something new at the end of the server's list, e.g. a reply in an oldest-first thread. */
  addLast(item: T): void {
    if (this.hasMore()) {
      this.tail.update((current) => [...current, item]);
    } else {
      this.loaded.update((current) => [...current, item]);
      this.offset++;
    }
  }

  remove(matches: (item: T) => boolean): void {
    const removed = this.loaded().filter(matches).length;
    this.loaded.update((current) => current.filter((item) => !matches(item)));
    this.tail.update((current) => current.filter((item) => !matches(item)));
    this.offset = Math.max(0, this.offset - removed);
  }

  private accept(page: T[]): void {
    const keys = new Set(this.loaded().map(this.keyOf));
    const fresh = page.filter((item) => {
      const key = this.keyOf(item);
      if (keys.has(key)) return false;
      keys.add(key);
      return true;
    });

    const returned = new Set(page.map(this.keyOf));
    this.loaded.update((current) => [...current, ...fresh]);
    this.tail.update((current) => current.filter((item) => !returned.has(this.keyOf(item))));

    // Counts what the server sent, duplicates included: they are positions in its list that are now covered.
    this.offset += page.length;
    this.hasMore.set(page.length >= this.pageSize);
  }
}
