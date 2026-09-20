import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { API, answerBackgroundRequests, expectPage, http, makePost, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { Post, User } from '../models/types';
import { ProfileComponent } from './profile';

describe('ProfileComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me', postsCount: 20 });
  const alice = makeUser({ id: 5, username: 'alice', displayName: 'Alice', email: '', postsCount: 26 });
  let harness: RouterTestingHarness;

  const el = () => harness.routeNativeElement as HTMLElement;
  const cards = () => [...el().querySelectorAll<HTMLElement>('.feed-posts .post-card')];
  const texts = () => cards().map((c) => c.querySelector('.post-text-content')?.textContent?.trim());
  const header = () => el().querySelector('.tweet-count')?.textContent?.trim();
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');
  const tab = (name: string) => [...el().querySelectorAll<HTMLElement>('.tab')].find((t) => t.textContent?.trim() === name)!;
  const settle = () => harness.fixture.whenStable();

  /** Newest first, by `author`. */
  const posts = (from: number, to: number, author: User = alice, overrides: Partial<Post> = {}) =>
    range(from, to).reverse().map((id) => makePost(id, { user: author, userId: author.id, ...overrides }));

  /** Navigates to a profile and answers the profile request; the posts are left for the test. */
  async function open(user: User = alice) {
    const navigation = harness.navigateByUrl(`/profile/${user.username}`, ProfileComponent);
    await settle();
    answerBackgroundRequests();
    http().expectOne(`${API}/users/profile/${user.username}`).flush(user);
    await navigation;
    await settle();
  }

  /** The cursor the API hands out after a page whose last entry is this post. */
  const after = (id: number) => `after-${id}`;

  /** A full page of 20 is assumed to have more after it, as the API would say; anything shorter is everything. */
  async function openWithPosts(first: Post[], user: User = alice, nextCursor: string | null = first.length >= 20 ? after(first[first.length - 1].id) : null) {
    await open(user);
    expectPage(`/posts/user/${user.id}`).flush(pageOf(first, nextCursor));
    await settle();
  }

  beforeEach(async () => {
    localStorage.clear();
    signInAs(me);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: provideAppTesting([{ path: 'profile/:username', component: ProfileComponent }]),
    });
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('Posts tab', () => {
    it("shows the profile's real post count from the start, not just what has been loaded", async () => {
      await openWithPosts(posts(7, 26));

      expect(cards()).toHaveLength(20);
      expect(header()).toBe('26 posts');
      expect(loadMoreButton()).not.toBeNull();
    });

    it('keeps the same count while the rest loads', async () => {
      await openWithPosts(posts(7, 26));

      loadMoreButton()!.click();
      expectPage('/posts/user/5', after(7)).flush(pageOf(posts(1, 6)));
      await settle();

      expect(cards()).toHaveLength(26);
      expect(header()).toBe('26 posts');
      expect(loadMoreButton()).toBeNull();
    });

    it('shows a short list without a button', async () => {
      await openWithPosts(posts(1, 3), { ...alice, postsCount: 3 });

      expect(header()).toBe('3 posts');
      expect(loadMoreButton()).toBeNull();
    });

    it.each([
      [0, '0 posts'],
      [1, '1 post'],
      [2, '2 posts'],
      [1234, '1234 posts'],
    ])('writes a count of %i as "%s"', async (postsCount, expected) => {
      await openWithPosts([], { ...alice, postsCount });

      expect(header()).toBe(expected);
    });

    it('says so when the user has not posted yet', async () => {
      await openWithPosts([], { ...alice, postsCount: 0 });

      expect(el().textContent).toContain("hasn't posted anything yet");
    });

    it('takes your own repost out of your profile when you undo it, and the next page still starts where it did', async () => {
      const repost = makePost(30, { user: alice, userId: alice.id, retweetedBy: me, isRetweeted: true });
      await openWithPosts([...posts(31, 49, me), repost], me);
      expect(cards()).toHaveLength(20);
      expect(header()).toBe('20 posts');

      cards()[19].querySelector<HTMLButtonElement>('.retweet-btn')!.click();
      http().expectOne(`${API}/retweets/toggle/30`).flush({ retweeted: false });
      await settle();
      expect(cards()).toHaveLength(19);
      expect(header()).toBe('19 posts'); // adjusted on the spot: no request that could overtake the undo

      loadMoreButton()!.click();
      expectPage('/posts/user/1', after(30)).flush(pageOf(posts(1, 10, me)));
      await settle();
      expect(cards()).toHaveLength(29);
    });

    it('removes a post you delete, and the next page still starts where it did', async () => {
      await openWithPosts(posts(30, 49, me), me);

      cards()[0].querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/49`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();
      expect(header()).toBe('20 posts'); // until the server has said what the new total is
      http().expectOne(`${API}/users/profile/me`).flush({ ...me, postsCount: 19 });
      await settle();
      expect(header()).toBe('19 posts');

      loadMoreButton()!.click();
      expectPage('/posts/user/1', after(30)).flush(pageOf([]));
      await settle();
      expect(cards()).toHaveLength(19);
    });

    it('keeps the posts and offers a retry when the next page fails', async () => {
      await openWithPosts(posts(7, 26));

      loadMoreButton()!.click();
      expectPage('/posts/user/5', after(7)).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(cards()).toHaveLength(20);
      expect(loadMoreButton()?.textContent?.trim()).toBe('Try again');
      expect(header()).toBe('26 posts');
    });

    it("takes the server's word for the new total when a delete removed more than one entry of yours", async () => {
      // A post you also reposted is two entries of yours; deleting it drops the count by two
      const both = [makePost(49, { user: me, userId: me.id, retweetedBy: me }), makePost(49, { user: me, userId: me.id })];
      await openWithPosts([...both, ...posts(31, 48, me)], me);

      cards()[1].querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/49`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();
      http().expectOne(`${API}/users/profile/me`).flush({ ...me, postsCount: 18 });
      await settle();

      expect(cards()).toHaveLength(18);
      expect(header()).toBe('18 posts');
    });

    it('never shows a negative count, even if the number it started from was out of date', async () => {
      const repost = makePost(30, { user: alice, userId: alice.id, retweetedBy: me, isRetweeted: true });
      await openWithPosts([repost], { ...me, postsCount: 0 });

      cards()[0].querySelector<HTMLButtonElement>('.retweet-btn')!.click();
      http().expectOne(`${API}/retweets/toggle/30`).flush({ retweeted: false });
      await settle();

      expect(header()).toBe('0 posts');
    });

    it('ignores a late count for a profile you have already left', async () => {
      await openWithPosts(posts(30, 49, me), me);
      cards()[0].querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/49`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();
      const late = http().expectOne(`${API}/users/profile/me`); // the new total is still on its way...

      const navigation = harness.navigateByUrl('/profile/alice', ProfileComponent);
      await settle();
      answerBackgroundRequests();
      http().expectOne(`${API}/users/profile/alice`).flush(alice);
      await navigation;
      await settle();
      expectPage('/posts/user/5').flush(pageOf(posts(7, 26)));
      await settle();
      late.flush({ ...me, postsCount: 19 }); // ...and arrives while Alice's profile is showing
      await settle();

      expect(header()).toBe('26 posts');
    });

    it("does not touch the count when you undo a repost on someone else's profile", async () => {
      // Undoing YOUR repost of a post shown on Alice's profile changes nothing there
      await openWithPosts([makePost(30, { user: alice, userId: alice.id, isRetweeted: true }), ...posts(7, 25)]);

      cards()[0].querySelector<HTMLButtonElement>('.retweet-btn')!.click();
      http().expectOne(`${API}/retweets/toggle/30`).flush({ retweeted: false });
      await settle();

      expect(cards()).toHaveLength(20);
      expect(header()).toBe('26 posts');
      http().expectNone(`${API}/users/profile/alice`);
    });
  });

  describe('Replies tab', () => {
    const replies = (from: number, to: number) =>
      posts(from, to, alice, { parentPostId: 900, replyToUsername: 'someone' }).map((p) => ({ ...p, content: `Reply #${p.id}` }));

    it('loads only when it is opened, and only once', async () => {
      await openWithPosts(posts(1, 3));
      http().expectNone((r) => r.url.endsWith('/replies'));

      tab('Replies').click();
      expectPage('/posts/user/5/replies').flush(pageOf(replies(1, 2)));
      await settle();
      expect(texts()).toEqual(['Reply #2', 'Reply #1']);

      tab('Posts').click();
      await settle();
      tab('Replies').click();
      await settle();

      http().expectNone((r) => r.url.endsWith('/replies'));
      expect(texts()).toEqual(['Reply #2', 'Reply #1']);
    });

    it('pages on its own, separately from the posts', async () => {
      await openWithPosts(posts(7, 26));
      tab('Replies').click();
      expectPage('/posts/user/5/replies').flush(pageOf(replies(11, 30), after(11)));
      await settle();
      expect(cards()).toHaveLength(20);

      loadMoreButton()!.click();
      expectPage('/posts/user/5/replies', after(11)).flush(pageOf(replies(1, 10)));
      await settle();
      expect(cards()).toHaveLength(30);
      expect(loadMoreButton()).toBeNull();

      tab('Posts').click();
      await settle();
      expect(cards()).toHaveLength(20); // still the first page of posts
      expect(header()).toBe('26 posts');
    });

    it('says so when there are no replies', async () => {
      await openWithPosts(posts(1, 3));

      tab('Replies').click();
      expectPage('/posts/user/5/replies').flush(pageOf([]));
      await settle();

      expect(el().textContent).toContain("hasn't replied to anyone yet");
    });

    it('tries again the next time the tab is opened if the first load failed', async () => {
      await openWithPosts(posts(1, 3));
      tab('Replies').click();
      expectPage('/posts/user/5/replies').flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
      await settle();

      tab('Posts').click();
      await settle();
      tab('Replies').click();

      expectPage('/posts/user/5/replies').flush(pageOf(replies(1, 2)));
      await settle();
      expect(cards()).toHaveLength(2);
    });
  });

  describe('going from one profile to another', () => {
    it('drops the posts still loading for the first one and shows the second one\'s', async () => {
      await open(alice);
      const stale = expectPage('/posts/user/5');

      const bob = makeUser({ id: 6, username: 'bob', displayName: 'Bob', email: '' });
      const navigation = harness.navigateByUrl('/profile/bob', ProfileComponent);
      await settle();
      answerBackgroundRequests();
      expect(stale.cancelled).toBe(true); // stopped as soon as the other profile was asked for, not only once it arrived
      http().expectOne(`${API}/users/profile/bob`).flush(bob);
      await navigation;
      await settle();

      expectPage('/posts/user/6').flush(pageOf(posts(1, 2, bob)));
      await settle();
      expect(el().querySelector('.header-titles h2')?.textContent?.trim()).toBe('Bob');
      expect(texts()).toEqual(['Post #2', 'Post #1']);
    });

    it('starts the new profile on its Posts tab with its own replies not yet loaded', async () => {
      await openWithPosts(posts(1, 2));
      tab('Replies').click();
      expectPage('/posts/user/5/replies').flush(pageOf([]));
      await settle();

      const bob = makeUser({ id: 6, username: 'bob', displayName: 'Bob', email: '' });
      const navigation = harness.navigateByUrl('/profile/bob', ProfileComponent);
      await settle();
      answerBackgroundRequests();
      http().expectOne(`${API}/users/profile/bob`).flush(bob);
      await navigation;
      await settle();
      expectPage('/posts/user/6').flush(pageOf(posts(1, 1, bob)));
      await settle();

      expect(tab('Posts').classList).toContain('active');
      http().expectNone((r) => r.url.endsWith('/replies'));

      // Bob's replies are loaded when asked for (not skipped because Alice's were), and Alice's are gone
      tab('Replies').click();
      expectPage('/posts/user/6/replies').flush(pageOf([makePost(50, { user: bob, userId: bob.id, content: 'Bob replies', parentPostId: 9 })]));
      await settle();
      expect(texts()).toEqual(['Bob replies']);
    });
  });

  it('says when the user does not exist', async () => {
    const navigation = harness.navigateByUrl('/profile/ghost', ProfileComponent);
    await settle();
    answerBackgroundRequests();

    http().expectOne(`${API}/users/profile/ghost`).flush({ message: 'not found' }, { status: 404, statusText: 'Not Found' });
    await navigation;
    await settle();

    expect(el().textContent).toContain('User not found');
  });
});
