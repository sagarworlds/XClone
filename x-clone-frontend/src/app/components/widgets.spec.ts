import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API, http, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { User } from '../models/types';
import { WidgetsComponent } from './widgets';

describe('WidgetsComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob', email: '' });
  const carol = makeUser({ id: 4, username: 'carol', displayName: 'Carol', email: '' });
  let fixture: ComponentFixture<WidgetsComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const settle = () => fixture.whenStable();
  const cards = () => [...el().querySelectorAll<HTMLElement>('.widget-card')];
  const card = (title: string) => cards().find((c) => c.querySelector('h3')?.textContent?.trim() === title);
  const names = (title: string) => [...(card(title)?.querySelectorAll('.display-name') ?? [])].map((n) => n.textContent?.trim());
  const suggestionsRequest = () => http().expectOne((r) => r.url === `${API}/users/suggestions`);

  async function open(suggestions: User[] | 'error' | 'pending' = []) {
    fixture = TestBed.createComponent(WidgetsComponent);
    fixture.detectChanges();
    await settle();
    if (suggestions === 'pending') return;
    const request = suggestionsRequest();
    expect(request.request.params.get('take')).toBe('4');
    if (suggestions === 'error') request.flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
    else request.flush(suggestions);
    await settle();
  }

  async function type(text: string) {
    const input = el().querySelector<HTMLInputElement>('.search-box input')!;
    input.value = text;
    input.dispatchEvent(new Event('input'));
    await settle();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(me);
    TestBed.configureTestingModule({ imports: [WidgetsComponent], providers: provideAppTesting() });
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('Who to follow', () => {
    it('says it is loading until the server answers', async () => {
      await open('pending');

      expect(card('Who to follow')?.textContent).toContain('Loading recommendations...');

      suggestionsRequest().flush([bob]);
      await settle();
      expect(names('Who to follow')).toEqual(['Bob']);
    });

    it('lists the suggestions, each with a Follow button', async () => {
      await open([bob, carol]);

      expect(names('Who to follow')).toEqual(['Bob', 'Carol']);
      const buttons = [...card('Who to follow')!.querySelectorAll('.follow-btn')].map((b) => b.textContent?.trim());
      expect(buttons).toEqual(['Follow', 'Follow']);
    });

    it('says so when there is nobody to suggest', async () => {
      await open([]);

      expect(card('Who to follow')?.textContent).toContain('No recommendations found.');
    });

    it('says so, and stops loading, when the suggestions cannot be fetched', async () => {
      await open('error');

      expect(card('Who to follow')?.textContent).toContain('No recommendations found.');
      expect(card('Who to follow')?.textContent).not.toContain('Loading');
    });

    it('follows someone from the list', async () => {
      await open([bob, carol]);

      card('Who to follow')!.querySelectorAll<HTMLButtonElement>('.follow-btn')[1].click();
      await settle();
      http().expectOne(`${API}/follows/toggle/4`).flush({ followed: true });
      await settle();

      const buttons = [...card('Who to follow')!.querySelectorAll('.follow-btn')].map((b) => b.textContent?.trim());
      expect(buttons).toEqual(['Follow', 'Following']);
    });
  });

  describe('search', () => {
    it('shows no results card until something is typed', async () => {
      await open();

      expect(card('Search Results')).toBeUndefined();
    });

    it('searches for what is typed and lists the people, without follow buttons', async () => {
      await open();

      await type('bo');
      const request = http().expectOne((r) => r.url === `${API}/users/search`);
      expect(request.request.params.get('query')).toBe('bo');
      request.flush([bob]);
      await settle();

      expect(names('Search Results')).toEqual(['Bob']);
      expect(card('Search Results')!.querySelector('.follow-btn')).toBeNull();
    });

    it('clears the results, without asking the server, when the box is emptied', async () => {
      await open();
      await type('bo');
      http().expectOne((r) => r.url === `${API}/users/search`).flush([bob]);
      await settle();

      await type('   ');

      http().expectNone((r) => r.url === `${API}/users/search`);
      expect(card('Search Results')).toBeUndefined();
    });
  });
});
