import { Observable } from 'rxjs';
import { Page } from '../models/types';
import { PagedList } from './paged-list';

interface Call {
  cursor: string | null;
  take: number;
  /** True when the list unsubscribed before an answer arrived. */
  cancelled: boolean;
  resolve(page: Page<number>): void;
  reject(): void;
}

const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => from + i);

/**
 * A fake endpoint that pages with cursors over `server`, the way the API does: the cursor names the last entry of the
 * previous page and a page is what comes strictly after it. `order` says which way the list is sorted (numbers newest
 * first, or oldest first like a thread). Tests change `server` to simulate what other people do meanwhile.
 * With `manual`, nothing is answered until the test calls resolve()/reject() on the recorded call.
 */
function setup(server: number[] = [], pageSize = 20, manual = false, order: 'newestFirst' | 'oldestFirst' = 'newestFirst') {
  const calls: Call[] = [];

  const pageAfter = (cursor: string | null, take: number): Page<number> => {
    const after = server.filter((n) => cursor === null || (order === 'newestFirst' ? n < Number(cursor) : n > Number(cursor)));
    const items = after.slice(0, take);
    return { items, nextCursor: after.length > take ? String(items[items.length - 1]) : null };
  };

  const list = new PagedList<number>(
    (cursor, take) =>
      new Observable<Page<number>>((subscriber) => {
        let answered = false;
        const call: Call = {
          cursor,
          take,
          cancelled: false,
          resolve: (page) => {
            answered = true;
            subscriber.next(page);
            subscriber.complete();
          },
          reject: () => {
            answered = true;
            subscriber.error(new Error('network down'));
          },
        };
        calls.push(call);
        if (!manual) call.resolve(pageAfter(cursor, take));
        return () => {
          call.cancelled = !answered;
        };
      }),
    (n) => String(n),
    pageSize,
  );
  return { list, calls, server };
}

const newestFirst = (from: number, to: number) => range(from, to).reverse();

describe('PagedList', () => {
  describe('paging', () => {
    it('loads page after page and stops when the server says there is no next page', () => {
      const { list } = setup(newestFirst(1, 45));

      list.loadFirst();
      expect(list.items()).toEqual(newestFirst(26, 45));
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.items()).toEqual(newestFirst(6, 45));
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.items()).toEqual(newestFirst(1, 45));
      expect(list.hasMore()).toBe(false);

      list.loadMore();
      expect(list.items()).toEqual(newestFirst(1, 45));
    });

    it('asks for each page with the cursor the previous one returned', () => {
      const { list, calls } = setup(newestFirst(1, 45));

      list.loadFirst();
      list.loadMore();
      list.loadMore();

      expect(calls.map((c) => [c.cursor, c.take])).toEqual([[null, 20], ['26', 20], ['6', 20]]);
    });

    it('needs no extra empty request after an exact multiple, because the server says the list ended', () => {
      const { list, calls } = setup(newestFirst(1, 40));

      list.loadFirst();
      list.loadMore();

      expect(list.hasMore()).toBe(false);
      expect(list.items()).toHaveLength(40);
      expect(calls).toHaveLength(2);
    });

    it('goes by what the server says about more pages, not by how full the page is', () => {
      const { list, calls } = setup([], 20, true);

      list.loadFirst();
      calls[0].resolve({ items: range(1, 20), nextCursor: null }); // a full page, but the end
      expect(list.hasMore()).toBe(false);

      list.loadFirst();
      calls[1].resolve({ items: [1, 2, 3], nextCursor: 'more' }); // a short page (e.g. entries filtered out), but not the end
      expect(list.hasMore()).toBe(true);
    });

    it('does nothing before the first page is there', () => {
      const { list, calls } = setup([], 20, true);

      list.loadMore();
      expect(calls).toHaveLength(0);

      list.loadFirst();
      list.loadMore(); // the first page is still on its way
      expect(calls).toHaveLength(1);
    });

    it('fetches once when Load more is pressed twice', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve({ items: range(1, 20), nextCursor: '20' });

      list.loadMore();
      list.loadMore();

      expect(calls).toHaveLength(2);
      expect(list.loadingMore()).toBe(true);
    });

    it('honours the page size it was given', () => {
      const { list, calls } = setup(newestFirst(1, 12), 5);

      list.loadFirst();
      list.loadMore();

      expect(list.items()).toEqual(newestFirst(3, 12));
      expect(calls[1]).toMatchObject({ cursor: '8', take: 5 });
    });
  });

  describe('when the list changes while the page is open', () => {
    it('is not disturbed when the user adds something at the top', () => {
      const { list, server, calls } = setup(newestFirst(1, 45));
      list.loadFirst(); // 45..26

      server.unshift(46);
      list.addFirst(46);
      list.loadMore();

      expect(calls[1].cursor).toBe('26'); // the same position, however much was added above it
      expect(list.items()).toEqual([46, ...newestFirst(6, 45)]);
    });

    it('is not disturbed when the user deletes a loaded entry', () => {
      const { list, server, calls } = setup(newestFirst(1, 45));
      list.loadFirst();

      server.splice(server.indexOf(40), 1);
      list.remove((n) => n === 40);
      list.loadMore();

      expect(calls[1].cursor).toBe('26');
      expect(list.items()).toEqual(newestFirst(6, 45).filter((n) => n !== 40));
    });

    it('still works when the entry the cursor points at is the one that gets deleted', () => {
      const { list, server } = setup(newestFirst(1, 45));
      list.loadFirst(); // the cursor points at 26

      server.splice(server.indexOf(26), 1);
      list.remove((n) => n === 26);
      list.loadMore();

      expect(list.items()).toEqual(newestFirst(6, 45).filter((n) => n !== 26)); // nothing skipped, nothing repeated
    });

    it('removes several matching entries (a post and its reposts) without any bookkeeping', () => {
      const { list, server } = setup(newestFirst(1, 60));
      list.loadFirst();
      const gone = (n: number) => n === 45 || n === 50 || n === 55;

      for (const n of [45, 50, 55]) server.splice(server.indexOf(n), 1);
      list.remove(gone);
      list.loadMore();

      expect(list.items()).toEqual(newestFirst(21, 60).filter((n) => !gone(n)));
    });

    it('repeats nothing when other people post in the meantime, however many they are', () => {
      const { list, server } = setup(newestFirst(1, 60));
      list.loadFirst(); // 60..41

      server.unshift(...newestFirst(101, 140)); // forty new posts by other people
      list.loadMore();

      expect(list.items()).toEqual(newestFirst(21, 60)); // the new ones wait for the next refresh; nothing is repeated or missing
      expect(list.hasMore()).toBe(true);
    });

    it('skips nothing when other people delete entries you have not reached yet', () => {
      const { list, server } = setup(newestFirst(1, 60));
      list.loadFirst();

      const gone = [30, 31, 32];
      for (const n of gone) server.splice(server.indexOf(n), 1);
      list.loadMore();

      // The next page is simply the next 20 entries that still exist
      expect(list.items()).toEqual([...newestFirst(41, 60), ...newestFirst(1, 40).filter((n) => !gone.includes(n)).slice(0, 20)]);
    });
  });

  describe('entries added at the end (a reply in an oldest-first thread)', () => {
    const thread = (count: number) => setup(range(1, count), 20, false, 'oldestFirst');

    it('keeps a new entry after the pages that are not loaded yet, and puts it in place once it is loaded', () => {
      const { list, server, calls } = thread(45);
      list.loadFirst();

      server.push(46);
      list.addLast(46);
      expect(list.items()).toEqual([...range(1, 20), 46]);

      list.loadMore();
      expect(calls[1].cursor).toBe('20'); // the reply does not move where the next page starts
      expect(list.items()).toEqual([...range(1, 40), 46]);

      list.loadMore();
      expect(list.items()).toEqual(range(1, 46));
      expect(list.hasMore()).toBe(false);
    });

    it('simply appends when everything is loaded already', () => {
      const { list, server } = thread(10);
      list.loadFirst();

      server.push(11);
      list.addLast(11);

      expect(list.items()).toEqual(range(1, 11));
      expect(list.hasMore()).toBe(false);
    });

    it('forgets such a pending entry when it is deleted again', () => {
      const { list, server } = thread(45);
      list.loadFirst();
      server.push(46);
      list.addLast(46);

      server.pop();
      list.remove((n) => n === 46);
      list.loadMore();

      expect(list.items()).toEqual(range(1, 40));
    });
  });

  describe('requests in flight', () => {
    it('cancels the previous request when loading again and ignores its late answer', () => {
      const { list, calls } = setup([], 20, true);

      list.loadFirst();
      list.loadFirst();
      expect(calls[0].cancelled).toBe(true);

      calls[0].resolve({ items: [1, 2, 3], nextCursor: null }); // the answer for what was on screen before
      expect(list.items()).toEqual([]);

      calls[1].resolve({ items: [7, 8], nextCursor: null });
      expect(list.items()).toEqual([7, 8]);
      expect(list.loading()).toBe(false);
    });

    it('reset() cancels what is running and clears everything', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve({ items: range(1, 20), nextCursor: '20' });
      list.loadMore();

      list.reset();

      expect(calls[1].cancelled).toBe(true);
      expect(list.items()).toEqual([]);
      expect(list.hasMore()).toBe(false);
      expect(list.loadingMore()).toBe(false);
    });

    it('starts from the beginning again after a reset, not from the old cursor', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve({ items: range(1, 20), nextCursor: '20' });

      list.loadFirst();

      expect(calls[1].cursor).toBeNull();
    });

    it('shows the first page as loading until it arrives', () => {
      const { list, calls } = setup([], 20, true);

      list.loadFirst();
      expect(list.loading()).toBe(true);

      calls[0].resolve({ items: [1], nextCursor: null });
      expect(list.loading()).toBe(false);
    });
  });

  describe('failures', () => {
    it('keeps what is loaded when a page fails, and a retry asks for the same page again', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve({ items: range(1, 20), nextCursor: '20' });

      list.loadMore();
      calls[1].reject();
      expect(list.failed()).toBe(true);
      expect(list.hasMore()).toBe(true);
      expect(list.loadingMore()).toBe(false);
      expect(list.items()).toEqual(range(1, 20));

      list.loadMore();
      expect(list.failed()).toBe(false);
      expect(calls[2].cursor).toBe('20');
      calls[2].resolve({ items: range(21, 25), nextCursor: null });
      expect(list.items()).toEqual(range(1, 25));
      expect(list.hasMore()).toBe(false);
    });

    it('reports a failed first page without claiming there is more', () => {
      const { list, calls } = setup([], 20, true);

      list.loadFirst();
      calls[0].reject();

      expect(list.failed()).toBe(true);
      expect(list.loading()).toBe(false);
      expect(list.hasMore()).toBe(false);
    });
  });

  describe('ensureLoaded()', () => {
    it('loads once, however often it is called', () => {
      const { list, calls } = setup([], 20, true);

      list.ensureLoaded();
      list.ensureLoaded();
      expect(calls).toHaveLength(1);

      calls[0].resolve({ items: [1], nextCursor: null });
      list.ensureLoaded();
      expect(calls).toHaveLength(1);
    });

    it('tries again when the first page failed', () => {
      const { list, calls } = setup([], 20, true);
      list.ensureLoaded();
      calls[0].reject();

      list.ensureLoaded();

      expect(calls).toHaveLength(2);
    });
  });

  it('drops an entry the server returns twice (a safety net)', () => {
    const { list, calls } = setup([], 20, true);
    list.loadFirst();
    calls[0].resolve({ items: [5, 4, 3], nextCursor: '3' });

    list.loadMore();
    calls[1].resolve({ items: [3, 2, 1], nextCursor: null });

    expect(list.items()).toEqual([5, 4, 3, 2, 1]);
  });
});
