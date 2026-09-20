import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UrlSegment } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API, answerBackgroundRequests, expectPage, http, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { followListMatcher } from '../app.routes';
import { User } from '../models/types';
import { FollowListComponent } from './follow-list';

@Component({ template: '', standalone: true })
class ElsewhereComponent {}

describe('FollowListComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  const alice = makeUser({ id: 5, username: 'alice', displayName: 'Alice', email: '' });
  const bob = makeUser({ id: 6, username: 'bob', displayName: 'Bob', email: '' });
  let harness: RouterTestingHarness;

  const el = () => harness.routeNativeElement as HTMLElement;
  const settle = () => harness.fixture.whenStable();
  const rows = () => [...el().querySelectorAll<HTMLElement>('app-user-row')];
  const names = () => rows().map((r) => r.querySelector('.display-name')?.textContent?.trim());
  const loadMoreButton = () => el().querySelector<HTMLButtonElement>('.load-more-btn');
  const tab = (name: string) => [...el().querySelectorAll<HTMLAnchorElement>('.tab')].find((t) => t.textContent?.trim() === name)!;
  const message = () => el().querySelector('.state-message')?.textContent?.replace(/\s+/g, ' ').trim();

  /** People with ids from..to, named "User <id>", newest follow first (highest id first), like the API. */
  const people = (from: number, to: number): User[] =>
    range(from, to).reverse().map((id) => makeUser({ id, username: `user${id}`, displayName: `User ${id}`, email: '' }));

  /** The cursor the API hands out after a page that ends at this id. */
  const after = (id: number) => `after-${id}`;

  /** Opens the page and answers the profile request; the list request is left for the test. */
  async function open(list: 'followers' | 'following' = 'followers', user: User = alice) {
    const navigation = harness.navigateByUrl(`/profile/${user.username}/${list}`, FollowListComponent);
    await settle();
    answerBackgroundRequests();
    http().expectOne(`${API}/users/profile/${user.username}`).flush(user);
    await navigation;
    await settle();
  }

  /** Opens the page with a first page of followers (or following) as the answer. */
  async function openWith(first: User[], nextCursor: string | null = null, list: 'followers' | 'following' = 'followers') {
    await open(list);
    expectPage(`/users/${alice.id}/${list}`).flush(pageOf(first, nextCursor));
    await settle();
  }

  beforeEach(async () => {
    localStorage.clear();
    signInAs(me);
    TestBed.configureTestingModule({
      imports: [FollowListComponent],
      providers: provideAppTesting([
        { matcher: followListMatcher, component: FollowListComponent },
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

  describe('the page', () => {
    it("shows whose list it is, with a way back to the profile", async () => {
      await openWith(people(10, 12));

      expect(el().querySelector('.header h2')?.textContent).toBe('Alice');
      expect(el().querySelector('.handle')?.textContent).toBe('@alice');
      expect(el().querySelector('a.back-btn')?.getAttribute('href')).toBe('/profile/alice');
    });

    it('opens on Followers for the followers address, and lists them newest first', async () => {
      await openWith(people(10, 12));

      expect(tab('Followers').classList).toContain('active');
      expect(tab('Following').classList).not.toContain('active');
      expect(names()).toEqual(['User 12', 'User 11', 'User 10']);
    });

    it('opens on Following for the following address, and asks for that list', async () => {
      await openWith(people(20, 21), null, 'following');

      expect(tab('Following').classList).toContain('active');
      expect(names()).toEqual(['User 21', 'User 20']);
    });

    it('links the two tabs to the two addresses', async () => {
      await openWith(people(10, 12));

      expect(tab('Followers').getAttribute('href')).toBe('/profile/alice/followers');
      expect(tab('Following').getAttribute('href')).toBe('/profile/alice/following');
    });

    it('says the user was not found, and asks for no lists', async () => {
      const navigation = harness.navigateByUrl('/profile/nobody/followers', FollowListComponent);
      await settle();
      answerBackgroundRequests();
      http().expectOne(`${API}/users/profile/nobody`).flush({ message: 'User not found' }, { status: 404, statusText: 'Not Found' });
      await navigation;
      await settle();

      expect(message()).toContain('User not found');
      expect(el().querySelector('.tabs')).toBeNull();
    });
  });

  describe('paging', () => {
    it('shows a Load more button when the server says there is more, and appends the next page', async () => {
      await openWith(people(11, 30), after(11));
      expect(rows()).toHaveLength(20);
      expect(loadMoreButton()).not.toBeNull();

      loadMoreButton()!.click();
      expectPage('/users/5/followers', after(11)).flush(pageOf(people(1, 10)));
      await settle();

      expect(rows()).toHaveLength(30);
      expect(names().at(-1)).toBe('User 1');
      expect(loadMoreButton()).toBeNull();
    });

    it('has no button when everything fit on one page', async () => {
      await openWith(people(1, 7));

      expect(rows()).toHaveLength(7);
      expect(loadMoreButton()).toBeNull();
    });

    it('keeps the people and lets you try again when the next page fails', async () => {
      await openWith(people(11, 30), after(11));

      loadMoreButton()!.click();
      expectPage('/users/5/followers', after(11)).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();
      expect(rows()).toHaveLength(20);
      expect(loadMoreButton()?.textContent?.trim()).toBe('Try again');

      loadMoreButton()!.click();
      expectPage('/users/5/followers', after(11)).flush(pageOf(people(1, 10)));
      await settle();
      expect(rows()).toHaveLength(30);
    });

    it('pages each list on its own', async () => {
      await openWith(people(11, 30), after(11));
      tab('Following').click();
      await settle();
      expectPage('/users/5/following').flush(pageOf(people(101, 120), 'following-cursor'));
      await settle();

      loadMoreButton()!.click();
      expectPage('/users/5/following', 'following-cursor').flush(pageOf(people(90, 95)));
      await settle();

      expect(rows()).toHaveLength(26);
    });
  });

  describe('empty and failing lists', () => {
    it('says there are no followers yet', async () => {
      await openWith([]);

      expect(message()).toContain('No followers yet');
      expect(message()).toContain('@alice');
    });

    it('says the user follows nobody yet', async () => {
      await openWith([], null, 'following');

      expect(message()).toContain('Not following anyone yet');
    });

    it('says when the list cannot be loaded', async () => {
      await open();
      expectPage('/users/5/followers').flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(message()).toContain("Couldn't load the list");
      expect(rows()).toHaveLength(0);
    });
  });

  describe('switching between the two lists', () => {
    it('loads the other list once, without fetching the profile or the first list again', async () => {
      await openWith(people(10, 12));

      tab('Following').click();
      await settle();
      http().expectNone(`${API}/users/profile/alice`);
      expectPage('/users/5/following').flush(pageOf(people(20, 21)));
      await settle();
      expect(names()).toEqual(['User 21', 'User 20']);
      expect(tab('Following').classList).toContain('active');

      // ...and back: both lists are kept, nothing is requested (the afterEach check would catch a request)
      tab('Followers').click();
      await settle();
      expect(names()).toEqual(['User 12', 'User 11', 'User 10']);
      tab('Following').click();
      await settle();
      expect(names()).toEqual(['User 21', 'User 20']);
    });
  });

  describe('the rows', () => {
    it('offers Follow next to everyone but you', async () => {
      await openWith([me, ...people(10, 11)]);

      const buttonsPerRow = rows().map((r) => r.querySelector('.follow-btn')?.textContent?.trim() ?? null);
      expect(names()[0]).toBe('Me');
      expect(buttonsPerRow).toEqual([null, 'Follow', 'Follow']); // no button on your own row
    });

    it('follows someone from the list', async () => {
      await openWith(people(10, 11));

      rows()[0].querySelector<HTMLButtonElement>('.follow-btn')!.click();
      await settle();
      http().expectOne(`${API}/follows/toggle/11`).flush({ followed: true });
      await settle();

      expect(rows()[0].querySelector('.follow-btn')?.textContent?.trim()).toBe('Following');
      expect(rows()[1].querySelector('.follow-btn')?.textContent?.trim()).toBe('Follow');
    });

    it('shows who you already follow as Following', async () => {
      await openWith([{ ...people(10, 10)[0], isFollowed: true }]);

      expect(rows()[0].querySelector('.follow-btn')?.textContent?.trim()).toBe('Following');
    });
  });

  describe('the sidebar', () => {
    const activeItem = () => el().querySelector('.nav-item.active')?.textContent?.trim();

    it("highlights Profile on your own lists", async () => {
      const navigation = harness.navigateByUrl('/profile/me/followers', FollowListComponent);
      await settle();
      answerBackgroundRequests();
      http().expectOne(`${API}/users/profile/me`).flush(me);
      await navigation;
      await settle();
      expectPage('/users/1/followers').flush(pageOf([]));
      await settle();

      expect(activeItem()).toContain('Profile');
    });

    it("highlights nothing on somebody else's lists", async () => {
      await openWith(people(10, 11));

      expect(activeItem()).toBeUndefined();
    });
  });

  describe('leaving the page', () => {
    it('cancels the profile request that is still on its way', async () => {
      const navigation = harness.navigateByUrl('/profile/alice/followers', FollowListComponent);
      await settle();
      answerBackgroundRequests();
      const profileRequest = http().expectOne(`${API}/users/profile/alice`);
      await navigation;

      await harness.navigateByUrl('/elsewhere');

      expect(profileRequest.cancelled).toBe(true);
    });

    it('cancels the list request that is still on its way', async () => {
      await open();
      const listRequest = expectPage('/users/5/followers');

      await harness.navigateByUrl('/elsewhere');

      expect(listRequest.cancelled).toBe(true);
    });
  });

  describe('going from one profile to another', () => {
    it("drops the first profile's answer, and shows the second one's lists", async () => {
      const navigation = harness.navigateByUrl('/profile/alice/followers', FollowListComponent);
      await settle();
      answerBackgroundRequests();
      const aliceProfile = http().expectOne(`${API}/users/profile/alice`); // still on its way
      await navigation;

      harness.navigateByUrl('/profile/bob/followers', FollowListComponent);
      await settle();
      expect(aliceProfile.cancelled).toBe(true);
      http().expectOne(`${API}/users/profile/bob`).flush(bob);
      await settle();
      expectPage('/users/6/followers').flush(pageOf(people(10, 11)));
      await settle();

      expect(el().querySelector('.header h2')?.textContent).toBe('Bob');
      expect(names()).toEqual(['User 11', 'User 10']);
    });

    it("starts the second profile's lists from scratch", async () => {
      await openWith(people(10, 12));

      harness.navigateByUrl('/profile/bob/followers', FollowListComponent);
      await settle();
      http().expectOne(`${API}/users/profile/bob`).flush(bob);
      await settle();
      expect(rows()).toHaveLength(0); // nothing of alice's is left on screen
      expectPage('/users/6/followers').flush(pageOf(people(30, 31)));
      await settle();

      expect(names()).toEqual(['User 31', 'User 30']);
    });
  });
});

describe('followListMatcher', () => {
  const segments = (...paths: string[]) => paths.map((p) => new UrlSegment(p, {}));

  it('matches the followers and following addresses of a profile, and names the parts', () => {
    const followers = followListMatcher(segments('profile', 'alice', 'followers'), null as never, null as never);
    const following = followListMatcher(segments('profile', 'alice', 'following'), null as never, null as never);

    expect(followers?.posParams?.['username'].path).toBe('alice');
    expect(followers?.posParams?.['list'].path).toBe('followers');
    expect(following?.posParams?.['list'].path).toBe('following');
  });

  it.each([
    ['another word at the end', ['profile', 'alice', 'media']],
    ['no list at all', ['profile', 'alice']],
    ['something after the list', ['profile', 'alice', 'followers', 'x']],
    ['another section', ['post', 'alice', 'followers']],
    ['just the list', ['followers']],
  ])('does not match %s', (_name, path) => {
    expect(followListMatcher(segments(...path), null as never, null as never)).toBeNull();
  });
});
