import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { API, http, provideAppTesting } from '../../testing/helpers';
import { RegisterComponent, registrationError } from './register';

describe('registrationError', () => {
  const general = 'Error occurred during registration. Try a different username/email.';

  it("gives the server's own message first", () => {
    expect(registrationError({ error: { message: 'Username already taken' } })).toBe('Username already taken');
  });

  it("gives the first validation message when the API answers with the standard list of errors", () => {
    expect(
      registrationError({
        error: { errors: { Username: ['Usernames can only contain letters, digits and underscores.'], Password: ['Too short.'] } },
      }),
    ).toBe('Usernames can only contain letters, digits and underscores.');
  });

  it('prefers a message over the list of errors', () => {
    expect(registrationError({ error: { message: 'Email already registered', errors: { Username: ['nope'] } } })).toBe('Email already registered');
  });

  it.each([
    ['nothing at all', {}],
    ['an empty body', { error: {} }],
    ['an empty message', { error: { message: '' } }],
    ['an empty list of errors', { error: { errors: {} } }],
    ['a list with no messages', { error: { errors: { Username: [] } } }],
  ])('says something general for %s', (_label, err) => {
    expect(registrationError(err)).toBe(general);
  });
});

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let navigate: ReturnType<typeof vi.spyOn>;

  const el = () => fixture.nativeElement as HTMLElement;
  const settle = () => fixture.whenStable();
  const field = (name: string) => el().querySelector<HTMLInputElement | HTMLTextAreaElement>(`[name=${name}]`)!;
  const submitButton = () => el().querySelector<HTMLButtonElement>('button[type=submit]')!;
  const banner = () => el().querySelector('.error-banner')?.textContent?.trim();
  const registerRequest = () => http().expectOne((r) => r.method === 'POST' && r.url === `${API}/auth/register`);

  async function type(name: string, value: string) {
    field(name).value = value;
    field(name).dispatchEvent(new Event('input'));
    await settle();
  }

  async function fillIn(username = 'johndoe') {
    await type('username', username);
    await type('displayName', 'John Doe');
    await type('email', 'john@example.test');
    await type('password', 'Passw0rd!x');
  }

  beforeEach(async () => {
    localStorage.clear();
    TestBed.configureTestingModule({ imports: [RegisterComponent], providers: provideAppTesting() });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    await settle();
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('the username', () => {
    it('is limited in the page to what the server accepts', () => {
      expect(field('username').getAttribute('pattern')).toBe('[A-Za-z0-9_]+');
      expect(field('username').getAttribute('minlength')).toBe('3');
      expect(field('username').getAttribute('maxlength')).toBe('50');
    });

    it('comes with a hint about what it may contain and why', () => {
      const hint = el().querySelector('#username-hint');

      expect(hint?.textContent).toContain('Letters, digits and underscores only');
      expect(hint?.textContent).toContain('@mention');
      expect(field('username').getAttribute('aria-describedby')).toBe('username-hint');
    });

    it.each(['john_doe', 'JohnDoe99', '123456', '___', 'abc'])('is accepted when it is %s', async (username) => {
      await fillIn(username);

      expect(submitButton().disabled).toBe(false);
    });

    it.each(['john doe', 'john-doe', 'john.doe', 'john@doe', 'jóhn', '東京東京', 'ab', 'a'.repeat(51)])(
      'keeps the form from being sent when it is %j',
      async (username) => {
        await fillIn(username);

        expect(submitButton().disabled).toBe(true);
      },
    );
  });

  describe('signing up', () => {
    it('sends what was typed, and goes home', async () => {
      await fillIn('john_doe');

      submitButton().click();
      const request = registerRequest();
      expect(request.request.body).toMatchObject({ username: 'john_doe', displayName: 'John Doe', email: 'john@example.test', password: 'Passw0rd!x' });
      request.flush({ id: 1, username: 'john_doe', email: 'john@example.test', displayName: 'John Doe', avatarUrl: '', token: 't', expiresAt: '2030-01-01T00:00:00Z' });
      await settle();

      expect(navigate).toHaveBeenCalledWith(['/home']);
    });

    it('cannot be sent twice while it is going', async () => {
      await fillIn();

      submitButton().click();
      await settle();
      expect(submitButton().disabled).toBe(true);
      expect(submitButton().textContent).toContain('Creating account...');
      registerRequest().flush({ id: 1, username: 'johndoe', email: '', displayName: '', avatarUrl: '', token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    });

    it("shows the server's message, and lets you try again", async () => {
      await fillIn('taken_name');

      submitButton().click();
      registerRequest().flush({ message: 'Username already taken' }, { status: 400, statusText: 'Bad Request' });
      await settle();

      expect(banner()).toBe('Username already taken');
      expect(submitButton().disabled).toBe(false);
      expect(navigate).not.toHaveBeenCalled();
    });

    it('shows the reason when the API rejects a rule, instead of a general hint', async () => {
      await fillIn();

      submitButton().click();
      registerRequest().flush(
        { title: 'One or more validation errors occurred.', errors: { Username: ['Usernames can only contain letters, digits and underscores.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle();

      expect(banner()).toBe('Usernames can only contain letters, digits and underscores.');
    });

    it('says something general when there is no reason to show', async () => {
      await fillIn();

      submitButton().click();
      registerRequest().error(new ProgressEvent('error'));
      await settle();

      expect(banner()).toContain('Error occurred during registration');
    });

    it('forgets the old complaint when it is sent again', async () => {
      await fillIn();
      submitButton().click();
      registerRequest().flush({ message: 'Username already taken' }, { status: 400, statusText: 'Bad Request' });
      await settle();

      submitButton().click();
      await settle();

      expect(banner()).toBeUndefined();
      registerRequest().flush({ id: 1, username: 'johndoe', email: '', displayName: '', avatarUrl: '', token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    });
  });
});
