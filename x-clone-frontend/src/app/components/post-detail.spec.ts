import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { API, answerBackgroundRequests, expectPage, http, makePost, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { Post } from '../models/types';
import { PostDetailComponent } from './post-detail';

describe('PostDetailComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  let harness: RouterTestingHarness;
  let component: PostDetailComponent;

  const el = () => harness.routeNativeElement as HTMLElement;
  const replyCards = () => [...el().querySelectorAll<HTMLElement>('.replies .post-card')];
  const replyTexts = () => replyCards().map((c) => c.querySelector('.post-text-content')?.textContent?.trim());
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');
  const settle = () => harness.fixture.whenStable();

  /** Oldest first, like the API. */
  const replies = (from: number, to: number, overrides: Partial<Post> = {}) =>
    range(from, to).map((id) => makePost(id, { parentPostId: 7, content: `Reply #${id}`, ...overrides }));

  /** Navigates to /post/:id and answers the post itself; the replies are left for the test. */
  async function open(id = 7, post: Post = makePost(id, { content: 'The post', repliesCount: 45 })) {
    const navigation = harness.navigateByUrl(`/post/${id}`, PostDetailComponent);
    await settle();
    answerBackgroundRequests();
    http().expectOne(`${API}/posts/${id}`).flush(post);
    component = await navigation;
    await settle();
  }

  /** The cursor the API hands out after a page whose last reply is this one. */
  const after = (id: number) => `after-${id}`;

  /** A full page of 20 is assumed to have more after it, as the API would say; anything shorter is everything. */
  async function openWithReplies(first: Post[], nextCursor: string | null = first.length >= 20 ? after(first[first.length - 1].id) : null) {
    await open();
    expectPage('/posts/7/replies').flush(pageOf(first, nextCursor));
    await settle();
  }

  /** Clicks Load more and answers the request, which must carry `cursor`. */
  async function clickLoadMore(cursor: string, answer: Post[], nextCursor: string | null = null) {
    loadMoreButton()!.click();
    expectPage('/posts/7/replies', cursor).flush(pageOf(answer, nextCursor));
    await settle();
  }

  beforeEach(async () => {
    localStorage.clear();
    signInAs(me);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({
      imports: [PostDetailComponent],
      providers: provideAppTesting([{ path: 'post/:id', component: PostDetailComponent }]),
    });
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('shows the post and its first 20 replies, oldest first, with Load more', async () => {
    await openWithReplies(replies(101, 120));

    expect(el().textContent).toContain('The post');
    expect(replyCards()).toHaveLength(20);
    expect(replyTexts()[0]).toBe('Reply #101');
    expect(replyTexts().at(-1)).toBe('Reply #120');
    expect(loadMoreButton()).not.toBeNull();
  });

  it('shows "Loading replies..." until the first page arrives', async () => {
    await open();

    expect(el().textContent).toContain('Loading replies...');
    expectPage('/posts/7/replies').flush(pageOf([]));
    await settle();
    expect(el().textContent).toContain('No replies yet');
  });

  it('appends the next pages in order and drops the button after the last', async () => {
    await openWithReplies(replies(101, 120));

    await clickLoadMore(after(120), replies(121, 140), after(140));
    await clickLoadMore(after(140), replies(141, 145));

    expect(replyTexts()).toEqual(replies(101, 145).map((r) => r.content));
    expect(loadMoreButton()).toBeNull();
  });

  describe('replying with images', () => {
    const image = `/uploads/${'b'.repeat(32)}.jpg`;

    it('uploads a chosen image, and sends its address with the reply', async () => {
      await openWithReplies(replies(101, 103));
      const input = el().querySelector<HTMLInputElement>('input[type=file]')!;
      Object.defineProperty(input, 'files', { value: [new File([new Uint8Array(3)], 'me.jpg', { type: 'image/jpeg' })], configurable: true });
      input.dispatchEvent(new Event('change'));
      http().expectOne(`${API}/media`).flush({ url: image });
      const textarea = el().querySelector('textarea')!;
      textarea.value = 'Look at this';
      textarea.dispatchEvent(new Event('input'));
      await settle();

      el().querySelector<HTMLButtonElement>('.publish-btn')!.click();
      const reply = http().expectOne((r) => r.method === 'POST' && r.url === `${API}/posts/7/replies`);
      expect(reply.request.body).toEqual({ content: 'Look at this', mediaUrls: [image] });
      reply.flush(makePost(200, { user: me, userId: me.id, parentPostId: 7, content: 'Look at this', mediaUrls: [image] }));
      await settle();

      expect(replyTexts().at(-1)).toBe('Look at this');
      expect(replyCards().at(-1)!.querySelector('.media-grid img')).not.toBeNull();
    });
  });

  describe('replying while more replies are still to be loaded', () => {
    const mine = () => makePost(200, { user: me, userId: me.id, parentPostId: 7, content: 'My reply' });

    it('shows your reply straight away, after the ones that are loaded', async () => {
      await openWithReplies(replies(101, 120));

      component.onReplyPosted(mine());
      await settle();

      expect(replyTexts()).toEqual([...replies(101, 120).map((r) => r.content), 'My reply']);
      expect(el().querySelector('.focus .comment-btn')?.textContent).toContain('46');
    });

    it('keeps asking from the same place, and keeps your reply at the end while the pages in between load', async () => {
      await openWithReplies(replies(101, 120));
      component.onReplyPosted(mine());
      await settle();

      await clickLoadMore(after(120), replies(121, 140), after(140)); // your reply does not change where the next page starts

      expect(replyTexts()).toEqual([...replies(101, 140).map((r) => r.content), 'My reply']);
    });

    it('puts your reply where it belongs once the page that contains it arrives, without showing it twice', async () => {
      await openWithReplies(replies(101, 120));
      component.onReplyPosted(mine());
      await settle();
      await clickLoadMore(after(120), replies(121, 140), after(140));

      await clickLoadMore(after(140), [...replies(141, 145), mine()]);

      expect(replyTexts()).toEqual([...replies(101, 145).map((r) => r.content), 'My reply']);
      expect(loadMoreButton()).toBeNull();
    });
  });

  it('adds your reply at the end when every reply is already loaded', async () => {
    await openWithReplies(replies(101, 103));

    component.onReplyPosted(makePost(200, { user: me, userId: me.id, parentPostId: 7, content: 'My reply' }));
    await settle();

    expect(replyTexts()).toEqual(['Reply #101', 'Reply #102', 'Reply #103', 'My reply']);
    expect(loadMoreButton()).toBeNull();
  });

  it('removes a reply you delete, lowers the count, and the next page still starts where it did', async () => {
    await openWithReplies([makePost(101, { user: me, userId: me.id, parentPostId: 7, content: 'Reply #101' }), ...replies(102, 120)]);

    replyCards()[0].querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
    http().expectOne(`${API}/posts/101`).flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(replyCards()).toHaveLength(19);
    expect(el().querySelector('.focus .comment-btn')?.textContent).toContain('44');
    await clickLoadMore(after(120), replies(121, 130));
    expect(replyCards()).toHaveLength(29);
  });

  it('says when the post is gone', async () => {
    const navigation = harness.navigateByUrl('/post/7', PostDetailComponent);
    await settle();
    answerBackgroundRequests();

    http().expectOne(`${API}/posts/7`).flush({ message: 'Post not found' }, { status: 404, statusText: 'Not Found' });
    await navigation;
    await settle();

    expect(el().textContent).toContain('Post not found');
    expect(replyCards()).toHaveLength(0);
  });

  it('links to the parent when the post is itself a reply', async () => {
    await open(8, makePost(8, { parentPostId: 3, content: 'A reply' }));
    expectPage('/posts/8/replies').flush(pageOf([]));
    await settle();

    expect(el().querySelector('.parent-link')?.getAttribute('href')).toBe('/post/3');
  });

  it('forgets the previous post\'s replies when you go to another post, even if they were still loading', async () => {
    await open();
    const pending = expectPage('/posts/7/replies');

    const navigation = harness.navigateByUrl('/post/8', PostDetailComponent);
    await settle();
    answerBackgroundRequests();
    expect(pending.cancelled).toBe(true); // stopped as soon as the other post was asked for
    http().expectOne(`${API}/posts/8`).flush(makePost(8, { content: 'Another post' }));
    await navigation;
    await settle();

    expectPage('/posts/8/replies').flush(pageOf(replies(1, 2).map((r) => ({ ...r, parentPostId: 8 }))));
    await settle();
    expect(el().textContent).toContain('Another post');
    expect(replyCards()).toHaveLength(2);
  });
});
