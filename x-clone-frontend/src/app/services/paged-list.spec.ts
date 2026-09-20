import { Observable } from 'rxjs';
import { PagedList } from './paged-list';

interface Call {
  skip: number;
  take: number;
  /** True when the list unsubscribed before an answer arrived. */
  cancelled: boolean;
  resolve(page: number[]): void;
  reject(): void;
}

const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => from + i);

/**
 * A fake endpoint. `server` is the list the API pages over (tests change it to simulate other people's activity).
 * With `manual`, nothing is answered until the test calls resolve()/reject() on the recorded call.
 */
function setup(server: number[] = [], pageSize = 20, manual = false) {
  const calls: Call[] = [];
  const list = new PagedList<number>(
    (skip, take) =>
      new Observable<number[]>((subscriber) => {
        let answered = false;
        const call: Call = {
          skip,
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
        if (!manual) call.resolve(server.slice(skip, skip + take));
        return () => {
          call.cancelled = !answered;
        };
      }),
    (n) => String(n),
    pageSize,
  );
  return { list, calls, server };
}

describe('PagedList', () => {
  describe('paging', () => {
    it('loads page after page and stops when a page comes back short', () => {
      const { list } = setup(range(1, 45));

      list.loadFirst();
      expect(list.items()).toEqual(range(1, 20));
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.items()).toEqual(range(1, 40));
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.items()).toEqual(range(1, 45));
      expect(list.hasMore()).toBe(false);

      list.loadMore();
      expect(list.items()).toEqual(range(1, 45));
    });

    it('asks for the next page at the right offset', () => {
      const { list, calls } = setup(range(1, 45));

      list.loadFirst();
      list.loadMore();

      expect(calls.map((c) => [c.skip, c.take])).toEqual([[0, 20], [20, 20]]);
    });

    it('needs one more (empty) request to learn that an exact multiple has ended', () => {
      const { list, calls } = setup(range(1, 40));

      list.loadFirst();
      list.loadMore();
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.hasMore()).toBe(false);
      expect(list.items()).toHaveLength(40);
      expect(calls).toHaveLength(3);
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
      calls[0].resolve(range(1, 20));

      list.loadMore();
      list.loadMore();

      expect(calls).toHaveLength(2);
      expect(list.loadingMore()).toBe(true);
    });

    it('honours the page size it was given', () => {
      const { list, calls } = setup(range(1, 12), 5);

      list.loadFirst();
      list.loadMore();

      expect(list.items()).toEqual(range(1, 10));
      expect(calls[1]).toMatchObject({ skip: 5, take: 5 });
    });
  });

  describe('when the list changes while the page is open', () => {
    it('does not repeat or skip anything after the user adds something at the top', () => {
      const { list, server } = setup(range(1, 45).reverse()); // 45..1
      list.loadFirst(); // 45..26

      server.unshift(46);
      list.addFirst(46);
      list.loadMore();

      expect(list.items()).toEqual([46, ...range(1, 45).reverse().slice(0, 40)]);
    });

    it('does not skip the next entry after the user deletes a loaded one', () => {
      const { list, server } = setup(range(1, 45));
      list.loadFirst();

      server.splice(server.indexOf(5), 1);
      list.remove((n) => n === 5);
      list.loadMore();

      expect(list.items()).toEqual(range(1, 40).filter((n) => n !== 5));
    });

    it('moves the offset by as many entries as were removed (a post and its reposts)', () => {
      const { list, server } = setup(range(1, 60));
      list.loadFirst();
      const gone = (n: number) => n === 3 || n === 7 || n === 11;

      for (const n of [3, 7, 11]) server.splice(server.indexOf(n), 1);
      list.remove(gone);
      list.loadMore();

      expect(list.items()).toEqual(range(1, 40).filter((n) => !gone(n)));
    });

    it('drops the one duplicate that appears when someone else posts in the meantime', () => {
      const { list, server } = setup(range(1, 45).reverse());
      list.loadFirst();

      server.unshift(99); // not known to the list
      list.loadMore();

      const items = list.items();
      expect(new Set(items).size).toBe(items.length);
      expect(items).toEqual(range(1, 45).reverse().slice(0, 39));
      expect(list.hasMore()).toBe(true);
    });

    it('moves on even when a whole page turns out to be duplicates, so Load more cannot get stuck', () => {
      const { list, server } = setup(range(1, 60).reverse()); // 60..1
      list.loadFirst(); // 60..41

      server.unshift(...range(101, 120)); // twenty new posts by other people
      list.loadMore(); // everything that comes back is already shown
      expect(list.items()).toEqual(range(41, 60).reverse());
      expect(list.hasMore()).toBe(true);

      list.loadMore();
      expect(list.items()).toEqual(range(21, 60).reverse());
    });
  });

  describe('entries added at the end (a reply in an oldest-first thread)', () => {
    it('keeps a new entry after the pages that are not loaded yet, and puts it in place once it is loaded', () => {
      const { list, server } = setup(range(1, 45));
      list.loadFirst();

      server.push(46);
      list.addLast(46);
      expect(list.items()).toEqual([...range(1, 20), 46]);

      list.loadMore();
      expect(list.items()).toEqual([...range(1, 40), 46]);

      list.loadMore();
      expect(list.items()).toEqual(range(1, 46));
      expect(list.hasMore()).toBe(false);
    });

    it('simply appends when everything is loaded already', () => {
      const { list, server } = setup(range(1, 10));
      list.loadFirst();

      server.push(11);
      list.addLast(11);

      expect(list.items()).toEqual(range(1, 11));
      expect(list.hasMore()).toBe(false);
    });

    it('leaves the offset alone when such a pending entry is deleted again', () => {
      const { list, server } = setup(range(1, 45));
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

      calls[0].resolve([1, 2, 3]); // the answer for what was on screen before
      expect(list.items()).toEqual([]);

      calls[1].resolve([7, 8]);
      expect(list.items()).toEqual([7, 8]);
      expect(list.loading()).toBe(false);
    });

    it('reset() cancels what is running and clears everything', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve(range(1, 20));
      list.loadMore();

      list.reset();

      expect(calls[1].cancelled).toBe(true);
      expect(list.items()).toEqual([]);
      expect(list.hasMore()).toBe(false);
      expect(list.loadingMore()).toBe(false);
    });

    it('shows the first page as loading until it arrives', () => {
      const { list, calls } = setup([], 20, true);

      list.loadFirst();
      expect(list.loading()).toBe(true);

      calls[0].resolve([1]);
      expect(list.loading()).toBe(false);
    });
  });

  describe('failures', () => {
    it('keeps what is loaded when a page fails, and a retry picks up where it left off', () => {
      const { list, calls } = setup([], 20, true);
      list.loadFirst();
      calls[0].resolve(range(1, 20));

      list.loadMore();
      calls[1].reject();
      expect(list.failed()).toBe(true);
      expect(list.hasMore()).toBe(true);
      expect(list.loadingMore()).toBe(false);
      expect(list.items()).toEqual(range(1, 20));

      list.loadMore();
      expect(list.failed()).toBe(false);
      expect(calls[2].skip).toBe(20);
      calls[2].resolve(range(21, 25));
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

      calls[0].resolve([1]);
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
});
