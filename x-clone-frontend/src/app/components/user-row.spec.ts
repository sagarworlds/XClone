import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { API, http, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { User } from '../models/types';
import { UserRowComponent } from './user-row';

describe('UserRowComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob', email: '' });
  let fixture: ComponentFixture<UserRowComponent>;
  let changes: User[];

  const el = () => fixture.nativeElement as HTMLElement;
  const settle = () => fixture.whenStable();
  const followButton = () => el().querySelector<HTMLButtonElement>('.follow-btn');
  const label = () => followButton()?.textContent?.trim();

  async function show(user: User, followButtonInput = true) {
    fixture = TestBed.createComponent(UserRowComponent);
    fixture.componentRef.setInput('user', user);
    fixture.componentRef.setInput('followButton', followButtonInput);
    changes = [];
    fixture.componentInstance.changed.subscribe((u) => changes.push(u));
    fixture.detectChanges();
    await settle();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(me);
    TestBed.configureTestingModule({ imports: [UserRowComponent], providers: provideAppTesting() });
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('what it shows', () => {
    it('shows the names', async () => {
      await show(bob);

      expect(el().querySelector('.display-name')?.textContent).toBe('Bob');
      expect(el().querySelector('.username')?.textContent).toBe('@bob');
    });

    it("uses the default avatar when there is none, and the person's own otherwise", async () => {
      await show(bob);
      expect(el().querySelector('img')?.getAttribute('src')).toContain('default_profile_images');

      await show({ ...bob, avatarUrl: 'https://img.test/bob.png' });
      expect(el().querySelector('img')?.getAttribute('src')).toBe('https://img.test/bob.png');
    });

    it('opens the profile when the person is clicked', async () => {
      const router = TestBed.inject(Router);
      const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      await show(bob);

      el().querySelector<HTMLElement>('.user-details')!.click();

      expect(navigateByUrl).toHaveBeenCalledOnce();
      expect(router.serializeUrl(navigateByUrl.mock.calls[0][0] as never)).toBe('/profile/bob');
    });

    it('says Follow for someone you do not follow, and Following for someone you do', async () => {
      await show(bob);
      expect(label()).toBe('Follow');
      expect(followButton()!.classList).not.toContain('following');

      await show({ ...bob, isFollowed: true });
      expect(label()).toBe('Following');
      expect(followButton()!.classList).toContain('following');
    });

    it('has no follow button on your own row', async () => {
      await show(me);

      expect(followButton()).toBeNull();
      expect(el().querySelector('.display-name')?.textContent).toBe('Me');
    });

    it('has no follow button where the row is only a link', async () => {
      await show(bob, false);

      expect(followButton()).toBeNull();
    });

    it('resets when the list hands it another person', async () => {
      await show(bob);
      fixture.componentRef.setInput('user', makeUser({ id: 4, username: 'dana', displayName: 'Dana', isFollowed: true }));
      await settle();

      expect(el().querySelector('.display-name')?.textContent).toBe('Dana');
      expect(label()).toBe('Following');
    });
  });

  describe('the follow button', () => {
    it('changes at once, then confirms with the server', async () => {
      await show(bob);

      followButton()!.click();
      await settle();
      expect(label()).toBe('Following'); // before the server has answered

      const request = http().expectOne(`${API}/follows/toggle/3`);
      expect(request.request.method).toBe('POST');
      request.flush({ followed: true });
      await settle();

      expect(label()).toBe('Following');
      expect(changes.map((u) => u.isFollowed)).toEqual([true]);
    });

    it('unfollows when you already follow', async () => {
      await show({ ...bob, isFollowed: true });

      followButton()!.click();
      await settle();
      expect(label()).toBe('Follow');

      http().expectOne(`${API}/follows/toggle/3`).flush({ followed: false });
      expect(changes.map((u) => u.isFollowed)).toEqual([false]);
    });

    it('puts everything back when the server fails', async () => {
      await show(bob);

      followButton()!.click();
      http().expectOne(`${API}/follows/toggle/3`).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(label()).toBe('Follow');
      expect(changes.map((u) => u.isFollowed)).toEqual([true, false]); // the list hears about both
    });

    it('does not open the profile', async () => {
      const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      await show(bob);

      followButton()!.click();
      http().expectOne(`${API}/follows/toggle/3`).flush({ followed: true });

      expect(navigateByUrl).not.toHaveBeenCalled();
    });
  });
});
