import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { RouterTestingHarness } from '@angular/router/testing';
import { API, answerBackgroundRequests, expectPage, http, makePost, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { Post } from '../models/types';
import { HashtagComponent } from './hashtag';

@Component({ template: '', standalone: true })
class ElsewhereComponent {}

describe('HashtagComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  let harness: RouterTestingHarness;

  const el = () => harness.routeNativeElement as HTMLElement;
  const settle = () => harness.fixture.whenStable();
  const cards = () => [...el().querySelectorAll<HTMLElement>('.post-card')];
  const texts = () => cards().map((c) => c.querySelector('.post-text-content')?.textContent?.trim());
  const title = () => el().querySelector('.header h2')?.textContent?.trim();
  const message = () => el().querySelector('.state-message')?.textContent?.replace(/\s+/g, ' ').trim();
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');

  /** Newest first, like the API. */
  const posts = (from: number, to: number, overrides: Partial<Post> = {}) =>
    range(from, to).reverse().map((id) => makePost(id, { content: `Post #${id} about #sunset`, ...overrides }));

  const after = (id: number) => `after-${id}`;

  /** Opens /hashtag/<address> and answers what the sidebar and widgets ask; the posts are left for the test. */
  async function open(address: string) {
    const navigation = harness.navigateByUrl(`/hashtag/${address}`, HashtagComponent);
    await settle();
    answerBackgroundRequests();
    await navigation;
    await settle();
  }

  async function openWith(address: string, path: string, first: Post[], nextCursor: string | null = null) {
    await open(address);
    expectPage(path).flush(pageOf(first, nextCursor));
    await settle();
  }

  beforeEach(async () => {
    localStorage.clear();
    signInAs(me);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({
      imports: [HashtagComponent],
      providers: provideAppTesting([
        { path: 'hashtag/:tag', component: HashtagComponent },
        { path: 'elsewhere', component: ElsewhereComponent },
      ]),
    });
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('the posts', () => {
    it('shows the tag and the posts that use it, in the order the server gives', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', posts(1, 3));

      expect(title()).toBe('#sunset');
      expect(texts()).toEqual(['Post #3 about #sunset', 'Post #2 about #sunset', 'Post #1 about #sunset']);
    });

    it('shows replies too, as replies', async () => {
      const reply = makePost(9, { content: 'a reply #sunset', parentPostId: 4, replyToUsername: 'other' });
      await openWith('sunset', '/posts/hashtag/sunset', [reply]);

      expect(cards()[0].textContent).toContain('Replying to @other');
    });

    it('links the tag inside each post to this same page', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', posts(1, 1));

      const link = cards()[0].querySelector<HTMLAnchorElement>('a.hashtag')!;
      expect(link.getAttribute('href')).toBe('/hashtag/sunset');
    });

    it('loads the next page with Load more, from where the last one ended', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', posts(6, 25), after(6));
      expect(cards()).toHaveLength(20);

      loadMoreButton()!.click();
      expectPage('/posts/hashtag/sunset', after(6)).flush(pageOf(posts(1, 5)));
      await settle();

      expect(cards()).toHaveLength(25);
      expect(loadMoreButton()).toBeNull();
    });

    it('has no button when everything fit on one page', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', posts(1, 4));

      expect(loadMoreButton()).toBeNull();
    });

    it('takes a post out when you delete it', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', [makePost(7, { user: me, userId: me.id, content: 'mine #sunset' }), ...posts(1, 2)]);

      el().querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/7`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();

      expect(cards()).toHaveLength(2);
      expect(texts()).not.toContain('mine #sunset');
    });
  });

  describe('the address', () => {
    it('is asked for as written, with other scripts made safe', async () => {
      await openWith('%E6%97%A5%E6%9C%AC%E8%AA%9E', '/posts/hashtag/%E6%97%A5%E6%9C%AC%E8%AA%9E', posts(1, 1));

      expect(title()).toBe('#日本語');
    });

    it('may be written with the # as well', async () => {
      await openWith('%23sunset', '/posts/hashtag/sunset', posts(1, 1));

      expect(title()).toBe('#sunset');
    });

    it('keeps the capitals it was written with in the title (the server ignores case)', async () => {
      await openWith('Sunset', '/posts/hashtag/Sunset', posts(1, 1));

      expect(title()).toBe('#Sunset');
    });

    it.each(['2026', 'a-b', 'a.b', '_', '%F0%9F%98%80'])('says %s is not a hashtag, without asking the server', async (address) => {
      await open(address);

      expect(message()).toContain('That is not a hashtag');
      expect(cards()).toHaveLength(0);
    });
  });

  describe('when there is nothing to show', () => {
    it('says nobody used the tag yet', async () => {
      await openWith('quiet', '/posts/hashtag/quiet', []);

      expect(message()).toContain('No posts with #quiet yet');
    });

    it('says when the posts cannot be loaded', async () => {
      await open('sunset');
      expectPage('/posts/hashtag/sunset').flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(message()).toContain("Couldn't load the posts");
    });

    it('keeps the posts and offers a retry when the next page fails', async () => {
      await openWith('sunset', '/posts/hashtag/sunset', posts(6, 25), after(6));

      loadMoreButton()!.click();
      expectPage('/posts/hashtag/sunset', after(6)).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(cards()).toHaveLength(20);
      expect(loadMoreButton()?.textContent?.trim()).toBe('Try again');
    });
  });

  describe('going from one hashtag to another', () => {
    it('drops the first one and its request, and shows the second', async () => {
      await open('first');
      const firstRequest = expectPage('/posts/hashtag/first');

      harness.navigateByUrl('/hashtag/second', HashtagComponent);
      await settle();
      expect(firstRequest.cancelled).toBe(true);
      expectPage('/posts/hashtag/second').flush(pageOf(posts(1, 2)));
      await settle();

      expect(title()).toBe('#second');
      expect(cards()).toHaveLength(2);
    });

    it('shows nothing of the first hashtag while the second one loads', async () => {
      await openWith('first', '/posts/hashtag/first', posts(1, 3));

      harness.navigateByUrl('/hashtag/second', HashtagComponent);
      await settle();

      expect(cards()).toHaveLength(0);
      expectPage('/posts/hashtag/second').flush(pageOf([]));
      await settle();
    });

    it('stops asking for a valid hashtag when it moves to one that is not', async () => {
      await open('first');
      const firstRequest = expectPage('/posts/hashtag/first');

      harness.navigateByUrl('/hashtag/2026', HashtagComponent);
      await settle();

      expect(firstRequest.cancelled).toBe(true);
      expect(message()).toContain('That is not a hashtag');
    });
  });

  describe('leaving the page', () => {
    it('cancels the request that is still on its way', async () => {
      await open('sunset');
      const request = expectPage('/posts/hashtag/sunset');

      await harness.navigateByUrl('/elsewhere');

      expect(request.cancelled).toBe(true);
    });
  });
});
