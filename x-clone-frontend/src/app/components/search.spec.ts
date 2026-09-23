import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { RouterTestingHarness } from '@angular/router/testing';
import { API, answerBackgroundRequests, expectPage, http, makePost, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { Post, User } from '../models/types';
import { SearchComponent } from './search';

@Component({ template: '', standalone: true })
class ElsewhereComponent {}

describe('SearchComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  let harness: RouterTestingHarness;

  const el = () => harness.routeNativeElement as HTMLElement;
  const settle = () => harness.fixture.whenStable();
  const cards = () => [...el().querySelectorAll<HTMLElement>('.post-card')];
  const texts = () => cards().map((c) => c.querySelector('.post-text-content')?.textContent?.trim());
  const rows = () => [...el().querySelectorAll<HTMLElement>('.user-row')];
  const names = () => rows().map((r) => r.querySelector('.display-name')?.textContent?.trim());
  const tabButtons = () => [...el().querySelectorAll<HTMLAnchorElement>('.tab')];
  /** The tabs marked active, by their text - exactly one is expected whenever tabs are shown at all. */
  const activeTabs = () => tabButtons().filter((t) => t.classList.contains('active')).map((t) => t.textContent?.trim());
  function activeTab(): string | undefined {
    expect(activeTabs()).toHaveLength(1);
    return activeTabs()[0];
  }
  const message = () => el().querySelector('.state-message')?.textContent?.replace(/\s+/g, ' ').trim();
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');

  /** Newest first, like the API. */
  const posts = (from: number, to: number, overrides: Partial<Post> = {}) =>
    range(from, to).reverse().map((id) => makePost(id, { content: `Post #${id} about coffee`, ...overrides }));

  const after = (id: number) => `after-${id}`;

  const expectUserSearch = (query: string) =>
    http().expectOne(
      (r) => r.method === 'GET' && r.url === `${API}/users/search` && r.params.get('query') === query,
      `GET /users/search?query=${query}`,
    );

  /** Opens /search at the given path (with or without ?q=), answering the sidebar's and widgets' own requests. */
  async function open(path: string) {
    const navigation = harness.navigateByUrl(path, SearchComponent);
    await settle();
    answerBackgroundRequests();
    await navigation;
    await settle();
  }

  /** Opens with a query and answers both the posts and the people it fires as soon as there is one. */
  async function openWith(query: string, firstPosts: Post[], people: User[] = [], nextCursor: string | null = null, tab: 'posts' | 'people' = 'posts') {
    await open(`/search?q=${encodeURIComponent(query)}${tab === 'people' ? '&tab=people' : ''}`);
    const postsRequest = expectPage('/posts/search');
    expect(postsRequest.request.params.get('query')).toBe(query);
    postsRequest.flush(pageOf(firstPosts, nextCursor));
    const peopleRequest = expectUserSearch(query);
    expect(peopleRequest.request.params.get('take')).toBe('20');
    peopleRequest.flush(people);
    await settle();
  }

  beforeEach(async () => {
    localStorage.clear();
    signInAs(me);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({
      imports: [SearchComponent],
      providers: provideAppTesting([
        { path: 'search', component: SearchComponent },
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

  describe('with no query', () => {
    it('invites you to search, and asks nothing of the server', async () => {
      await open('/search');

      expect(message()).toContain('Search XClone');
      expect(tabButtons()).toHaveLength(0);
      http().expectNone((r) => r.url === `${API}/posts/search`);
      http().expectNone((r) => r.url === `${API}/users/search`);
    });

    it('shows the invitation even when the address also names a tab', async () => {
      await open('/search?tab=people');

      expect(message()).toContain('Search XClone');
      expect(tabButtons()).toHaveLength(0);
    });
  });

  describe('a query with spaces around it', () => {
    it('is trimmed before it is sent, and in the heading', async () => {
      await open('/search?q=%20%20coffee%20%20');

      const postsRequest = expectPage('/posts/search');
      expect(postsRequest.request.params.get('query')).toBe('coffee');
      postsRequest.flush(pageOf([]));
      expectUserSearch('coffee').flush([]);
      await settle();

      expect(el().querySelector('.handle')?.textContent?.trim()).toBe('coffee');
    });
  });

  describe('the Posts tab', () => {
    it('is the one shown by default, with the query sent to the server and the results listed', async () => {
      await openWith('coffee', posts(1, 3));

      expect(activeTab()).toBe('Posts');
      expect(texts()).toEqual(['Post #3 about coffee', 'Post #2 about coffee', 'Post #1 about coffee']);
    });

    it('shows replies too, as replies', async () => {
      const reply = makePost(9, { content: 'a reply about coffee', parentPostId: 4, replyToUsername: 'other' });
      await openWith('coffee', [reply]);

      expect(cards()[0].textContent).toContain('Replying to @other');
    });

    it('loads the next page with Load more, from where the last one ended', async () => {
      await openWith('coffee', posts(6, 25), [], after(6));
      expect(cards()).toHaveLength(20);

      loadMoreButton()!.click();
      expectPage('/posts/search', after(6)).flush(pageOf(posts(1, 5)));
      await settle();

      expect(cards()).toHaveLength(25);
      expect(loadMoreButton()).toBeNull();
    });

    it('takes a post out when you delete it', async () => {
      await openWith('coffee', [makePost(7, { user: me, userId: me.id, content: 'mine, about coffee' }), ...posts(1, 2)]);

      el().querySelector<HTMLButtonElement>('.delete-post-btn')!.click();
      http().expectOne(`${API}/posts/7`).flush(null, { status: 204, statusText: 'No Content' });
      await settle();

      expect(cards()).toHaveLength(2);
      expect(texts()).not.toContain('mine, about coffee');
    });

    it('says so when nothing matches', async () => {
      await openWith('coffee', []);

      expect(message()).toContain('No posts found');
      expect(message()).toContain('coffee');
    });

    it('says so when the results cannot be loaded', async () => {
      await open('/search?q=coffee');
      expectPage('/posts/search').flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      expectUserSearch('coffee').flush([]);
      await settle();

      expect(message()).toContain("Couldn't load the results");
    });
  });

  describe('the People tab', () => {
    it('lists whoever the same query finds, with follow buttons', async () => {
      const bob = makeUser({ id: 2, username: 'bob', displayName: 'Bob', email: '' });
      await openWith('coffee', [], [bob], null, 'people');

      expect(activeTab()).toBe('People');
      expect(names()).toEqual(['Bob']);
      expect(el().querySelector('.follow-btn')).not.toBeNull();
    });

    it('says so when nobody matches', async () => {
      await openWith('coffee', [], [], null, 'people');

      expect(message()).toContain('No people found');
    });

    it('says so when the results cannot be loaded', async () => {
      await open('/search?q=coffee&tab=people');
      expectPage('/posts/search').flush(pageOf([]));
      expectUserSearch('coffee').flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(message()).toContain("Couldn't load the results");
    });

    it('has no Load more (the search is not paged)', async () => {
      const many = range(1, 20).map((id) => makeUser({ id, username: `u${id}`, displayName: `U${id}`, email: '' }));
      await openWith('coffee', [], many, null, 'people');

      expect(loadMoreButton()).toBeNull();
    });

    it('forgets a failure once a later query succeeds, even when that one also finds nobody', async () => {
      await open('/search?q=coffee&tab=people');
      expectPage('/posts/search').flush(pageOf([]));
      expectUserSearch('coffee').flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();
      expect(message()).toContain("Couldn't load the results");

      harness.navigateByUrl('/search?q=tea&tab=people', SearchComponent);
      await settle();
      expectPage('/posts/search').flush(pageOf([]));
      expectUserSearch('tea').flush([]); // succeeds, just finds nobody - a different message from the failure
      await settle();

      expect(message()).toContain('No people found');
      expect(message()).not.toContain("Couldn't load");
    });
  });

  describe('switching tabs', () => {
    it('shows the other tab\'s results without asking the server again', async () => {
      const bob = makeUser({ id: 2, username: 'bob', displayName: 'Bob', email: '' });
      await openWith('coffee', posts(1, 1), [bob]);

      tabButtons().find((t) => t.textContent?.trim() === 'People')!.click();
      await settle();

      expect(activeTab()).toBe('People');
      expect(names()).toEqual(['Bob']);
      http().expectNone((r) => r.url === `${API}/users/search`);

      tabButtons().find((t) => t.textContent?.trim() === 'Posts')!.click();
      await settle();

      expect(activeTab()).toBe('Posts');
      expect(texts()).toEqual(['Post #1 about coffee']);
      http().expectNone((r) => r.url === `${API}/posts/search`);
    });

    it('goes straight to the People tab when the address says so', async () => {
      await openWith('coffee', [], [], null, 'people');

      expect(activeTab()).toBe('People');
    });
  });

  describe('changing the query', () => {
    it('drops the old results and their requests, and asks for the new query on both tabs', async () => {
      await open('/search?q=coffee');
      const oldPosts = expectPage('/posts/search');
      const oldPeople = expectUserSearch('coffee');

      harness.navigateByUrl('/search?q=tea', SearchComponent);
      await settle();

      expect(oldPosts.cancelled).toBe(true);
      expect(oldPeople.cancelled).toBe(true);
      expect(cards()).toHaveLength(0);

      const newPosts = expectPage('/posts/search');
      expect(newPosts.request.params.get('query')).toBe('tea');
      newPosts.flush(pageOf([makePost(9, { content: 'a nice tea' })]));
      expectUserSearch('tea').flush([]);
      await settle();

      expect(texts()).toEqual(['a nice tea']);
    });

    it('going back to an empty query shows the invitation again and asks nothing', async () => {
      await openWith('coffee', posts(1, 1));

      harness.navigateByUrl('/search', SearchComponent);
      await settle();

      expect(message()).toContain('Search XClone');
      http().expectNone((r) => r.url === `${API}/posts/search`);
      http().expectNone((r) => r.url === `${API}/users/search`);
    });
  });

  describe('leaving the page', () => {
    it('cancels the posts and the people requests that are still on their way', async () => {
      await open('/search?q=coffee');
      const postsRequest = expectPage('/posts/search');
      const peopleRequest = expectUserSearch('coffee');

      await harness.navigateByUrl('/elsewhere');

      expect(postsRequest.cancelled).toBe(true);
      expect(peopleRequest.cancelled).toBe(true);
    });
  });
});
