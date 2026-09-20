import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API, http, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { ApiService } from '../services/api.service';
import { NotificationsService } from '../services/notifications.service';
import { SidebarComponent } from './sidebar';

describe('SidebarComponent', () => {
  let fixture: ComponentFixture<SidebarComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const badge = () => el().querySelector('.badge');
  const notificationsLink = () => [...el().querySelectorAll('a.nav-item')].find((a) => a.textContent?.includes('Notifications')) as HTMLAnchorElement;

  /** Renders the sidebar; the unread count the server reports is `unread`. */
  async function render(unread: number) {
    fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http().expectOne(`${API}/notifications/unread-count`).flush({ count: unread });
    await fixture.whenStable();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(makeUser({ username: 'me', displayName: 'Me' }));
    TestBed.configureTestingModule({ imports: [SidebarComponent], providers: provideAppTesting() });
  });

  afterEach(() => localStorage.clear());

  it('links to Notifications', async () => {
    await render(0);

    expect(notificationsLink().getAttribute('href')).toBe('/notifications');
  });

  it('shows how many notifications are unread', async () => {
    await render(5);

    expect(badge()?.textContent?.trim()).toBe('5');
    expect(badge()?.getAttribute('aria-label')).toBe('5 unread');
  });

  it('shows no badge when everything has been read', async () => {
    await render(0);

    expect(badge()).toBeNull();
  });

  it('caps the badge at 99+', async () => {
    await render(150);

    expect(badge()?.textContent?.trim()).toBe('99+');
  });

  it('shows 99 as it is', async () => {
    await render(99);

    expect(badge()?.textContent?.trim()).toBe('99');
  });

  it('follows the count as it changes', async () => {
    await render(3);

    // Someone opens the notifications page, which clears the badge
    TestBed.inject(NotificationsService).unreadCount.set(0);
    await fixture.whenStable();

    expect(badge()).toBeNull();
  });

  it('highlights the item of the page it is on', async () => {
    fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('active', 'notifications');
    fixture.detectChanges();
    await fixture.whenStable();
    http().expectOne(`${API}/notifications/unread-count`).flush({ count: 0 });
    await fixture.whenStable();

    expect(notificationsLink().classList).toContain('active');
    const home = [...el().querySelectorAll('a.nav-item')].find((a) => a.textContent?.includes('Home'));
    expect(home?.classList).not.toContain('active');
  });

  it('logs out from the Log out button', async () => {
    await render(0);
    const logout = vi.spyOn(TestBed.inject(ApiService), 'logout').mockImplementation(() => {});

    (el().querySelector('.logout-btn') as HTMLButtonElement).click();

    expect(logout).toHaveBeenCalledTimes(1);
  });
});
