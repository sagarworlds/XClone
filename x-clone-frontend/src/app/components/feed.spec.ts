import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API, answerBackgroundRequests, expectPage, http, makePost, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { Post } from '../models/types';
import { FeedComponent } from './feed';

describe('FeedComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  let fixture: ComponentFixture<FeedComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const cards = () => [...el().querySelectorAll<HTMLElement>('.post-card')];
  const texts = () => cards().map((c) => c.querySelector('.post-text-content')?.textContent?.trim());
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');
  const settle = () => fixture.whenStable();

  /** Newest first, like the API: ids from `to` down to `from`. */
  const posts = (from: number, to: number, overrides: Partial<Post> = {}) =>
    range(from, to).reverse().map((id) => makePost(id, overrides));

  /** The cursor the API hands out after a page that ends at this post id. */
  const after = (id: number) => `after-${id}`;

  /** Opens the feed; `nextCursor` is what the server says about a next page (null: this is everything). */
  async function open(firstPage: Post[], nextCursor: string | null = null) {
    fixture = TestBed.createComponent(FeedComponent);
    fixture.detectChanges();
    await settle();
    answerBackgroundRequests();
    expectPage('/posts/feed').flush(pageOf(firstPage, nextCursor));
    await settle();
  }

  /** Clicks Load more and answers the request, which must carry `cursor`. */
  async function clickLoadMore(cursor: string, answer: Post[], nextCursor: string | null = null) {
    loadMoreButton()!.click();
    expectPage('/posts/feed', cursor).flush(pageOf(answer, nextCursor));
    await settle();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(me);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({ imports: [FeedComponent], providers: provideAppTesting() });
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('shows the first 20 posts and a Load more button', async () => {
    await open(posts(26, 45), after(26));

    expect(cards()).toHaveLength(20);
    expect(texts()[0]).toBe('Post #45');
    expect(loadMoreButton()?.textContent?.trim()).toBe('Load more');
  });

  it('appends the next page, and the button goes away after the last one', async () => {
    await open(posts(26, 45), after(26));

    await clickLoadMore(after(26), posts(6, 25), after(6));
    expect(cards()).toHaveLength(40);
    expect(loadMoreButton()).not.toBeNull();

    await clickLoadMore(after(6), posts(1, 5));
    expect(cards()).toHaveLength(45);
    expect(texts().at(-1)).toBe('Post #1');
    expect(loadMoreButton()).toBeNull();
  });

  it('has no button when the server says that was everything', async () => {
    await open(posts(1, 7));

    expect(cards()).toHaveLength(7);
    expect(loadMoreButton()).toBeNull();
  });

  it('has no button after a full page that the server says was the last one', async () => {
    await open(posts(26, 45)); // exactly one page of 20 and nothing after it

    expect(cards()).toHaveLength(20);
    expect(loadMoreButton()).toBeNull();
  });

  it("shows a post twice when it is there both as the original and as someone's repost", async () => {
    const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob' });
    const warn = vi.spyOn(console, 'warn');
    const error = vi.spyOn(console, 'error');

    await open([makePost(30, { retweetedBy: bob }), makePost(30), makePost(29)]);

    expect(cards()).toHaveLength(3);
    expect(cards()[0].textContent).toContain('Bob reposted');
    expect(cards()[1].textContent).not.toContain('reposted');
    expect(warn).not.toHaveBeenCalled(); // Angular warns about duplicate @for keys
    expect(error).not.toHaveBeenCalled();
  });

  it('welcomes a new user whose timeline is empty', async () => {
    await open([]);

    expect(cards()).toHaveLength(0);
    expect(el().textContent).toContain('Welcome to X!');
  });

  describe('while you use the page in between', () => {
    // A cursor is a position in the server's list, so nothing done here changes where the next page starts.

    it('puts a new post on top, and the next page still starts where it did', async () => {
      await open(posts(26, 45), after(26));
      const textarea = el().querySelector('textarea')!;
      textarea.value = 'Brand new';
      textarea.dispatchEvent(new Event('input'));
      await settle();

      el().querySelector<HTMLButtonElement>('.publish-btn')!.click();
      const create = http().expectOne((r) => r.method === 'POST' && r.url === `${API}/posts`);
      expect(create.request.body).toMatchObject({ content: 'Brand new' });
      create.flush(makePost(99, { user: me, userId: me.id, content: 'Brand new' }));
      await settle();
      expect(texts()[0]).toBe('Brand new');
      expect(cards()).toHaveLength(21);

      await clickLoadMore(after(26), posts(6, 25));
      expect(texts()).toEqual(['Brand new', ...posts(6, 45).map((p) => p.content)]);
    });

    it('removes a post you delete, and the next page still starts where it did', async () => {
      await open([makePost(45, { user: me, userId: me.id }), ...posts(26, 44)], after(26));

      el().querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/45`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();
      expect(cards()).toHaveLength(19);
      expect(texts()).not.toContain('Post #45');

      await clickLoadMore(after(26), posts(6, 25));
      expect(cards()).toHaveLength(39);
    });

    it('removes every entry of a post you delete, the reposts of it included', async () => {
      const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob' });
      await open(
        [
          makePost(45, { user: me, userId: me.id, retweetedBy: bob }),
          makePost(45, { user: me, userId: me.id }),
          ...posts(27, 44),
        ],
        after(27),
      );
      expect(cards()).toHaveLength(20);

      cards()[1].querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/45`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();

      expect(cards()).toHaveLength(18);
      expect(texts()).not.toContain('Post #45');
      await clickLoadMore(after(27), posts(6, 26));
    });

    it('takes your own repost out of the list when you undo it', async () => {
      const someoneElses = makePost(30, { retweetedBy: me, isRetweeted: true });
      await open([...posts(31, 45), someoneElses, ...posts(26, 29)], after(26));
      expect(cards()).toHaveLength(20);

      const entry = cards()[15];
      expect(entry.textContent).toContain('You reposted');
      entry.querySelector<HTMLButtonElement>('.retweet-btn')!.click();
      http().expectOne(`${API}/retweets/toggle/30`).flush({ retweeted: false });
      await settle();

      expect(cards()).toHaveLength(19);
      expect(el().textContent).not.toContain('You reposted');
      await clickLoadMore(after(26), posts(6, 25));
    });

    it('keeps the entry when someone else undoes their repost of it', async () => {
      const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob' });
      await open([makePost(30, { retweetedBy: bob, isRetweeted: true }), ...posts(31, 45)]);

      cards()[0].querySelector<HTMLButtonElement>('.retweet-btn')!.click();
      http().expectOne(`${API}/retweets/toggle/30`).flush({ retweeted: false });
      await settle();

      expect(cards()).toHaveLength(16);
    });

    it('does not move the list when you like a post', async () => {
      await open(posts(26, 45), after(26));

      cards()[3].querySelector<HTMLButtonElement>('.like-btn')!.click();
      http().expectOne(`${API}/likes/toggle/42`).flush({ liked: true });
      await settle();

      expect(cards()).toHaveLength(20);
      await clickLoadMore(after(26), posts(6, 25));
    });
  });

  describe('posting with images', () => {
    const image = `/uploads/${'a'.repeat(32)}.png`;

    it('uploads a chosen image, and sends its address with the post', async () => {
      await open(posts(26, 45), after(26));
      const input = el().querySelector<HTMLInputElement>('input[type=file]')!;
      Object.defineProperty(input, 'files', { value: [new File([new Uint8Array(3)], 'holiday.png', { type: 'image/png' })], configurable: true });
      input.dispatchEvent(new Event('change'));
      http().expectOne(`${API}/media`).flush({ url: image });
      const textarea = el().querySelector('textarea')!;
      textarea.value = 'With a picture';
      textarea.dispatchEvent(new Event('input'));
      await settle();

      el().querySelector<HTMLButtonElement>('.publish-btn')!.click();
      const create = http().expectOne((r) => r.method === 'POST' && r.url === `${API}/posts`);
      expect(create.request.body).toEqual({ content: 'With a picture', mediaUrls: [image] });
      create.flush(makePost(99, { user: me, userId: me.id, content: 'With a picture', mediaUrls: [image] }));
      await settle();

      expect(texts()[0]).toBe('With a picture');
      expect(cards()[0].querySelector('.media-grid img')?.getAttribute('src')).toBe(`http://localhost:5168${image}`);
    });
  });

  describe('when loading more goes wrong', () => {
    it('keeps the posts, explains, and lets you try again for the same page', async () => {
      await open(posts(26, 45), after(26));

      loadMoreButton()!.click();
      expectPage('/posts/feed', after(26)).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(cards()).toHaveLength(20);
      expect(el().textContent).toContain("Couldn't load more");
      expect(loadMoreButton()?.textContent?.trim()).toBe('Try again');

      await clickLoadMore(after(26), posts(6, 25));
      expect(cards()).toHaveLength(40);
      expect(el().textContent).not.toContain("Couldn't load more");
    });
  });
});
