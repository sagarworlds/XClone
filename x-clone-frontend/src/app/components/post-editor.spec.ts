import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PostEditorComponent } from './post-editor';

describe('PostEditorComponent', () => {
  let fixture: ComponentFixture<PostEditorComponent>;
  let submitted: string[];
  let cancelled: number;

  const el = () => fixture.nativeElement as HTMLElement;
  const box = () => el().querySelector<HTMLTextAreaElement>('textarea')!;
  const saveButton = () => el().querySelector<HTMLButtonElement>('.editor-save')!;
  const cancelButton = () => el().querySelector<HTMLButtonElement>('.editor-cancel')!;
  const counter = () => el().querySelector('.editor-counter')!;
  const settle = () => fixture.whenStable();

  async function open(content: string, inputs: { saving?: boolean; error?: string | null } = {}) {
    fixture = TestBed.createComponent(PostEditorComponent);
    fixture.componentRef.setInput('content', content);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    submitted = [];
    cancelled = 0;
    fixture.componentInstance.submitted.subscribe((text) => submitted.push(text));
    fixture.componentInstance.cancelled.subscribe(() => cancelled++);
    fixture.detectChanges();
    await settle();
  }

  async function type(text: string) {
    box().value = text;
    box().dispatchEvent(new Event('input'));
    await settle();
  }

  const press = (init: KeyboardEventInit) => box().dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, ...init }));

  beforeEach(() => TestBed.configureTestingModule({ imports: [PostEditorComponent] }));

  describe('the box', () => {
    it('starts with the text of the post, ready to type, with the cursor after it', async () => {
      await open('Hello world');

      expect(box().value).toBe('Hello world');
      expect(document.activeElement).toBe(box());
      expect([box().selectionStart, box().selectionEnd]).toEqual([11, 11]);
    });

    it('is named for people who cannot see it, and cannot take more than a post can hold', async () => {
      await open('x');

      expect(box().getAttribute('aria-label')).toBe('Edit your post');
      expect(box().getAttribute('maxlength')).toBe('280');
    });

    it('counts the characters as they are typed, in red from 251', async () => {
      await open('abc');
      expect(counter().textContent?.trim()).toBe('3/280');
      expect(counter().classList).not.toContain('warning');

      await type('a'.repeat(250));
      expect(counter().textContent?.trim()).toBe('250/280');
      expect(counter().classList).not.toContain('warning');

      await type('a'.repeat(251));
      expect(counter().textContent?.trim()).toBe('251/280');
      expect(counter().classList).toContain('warning');
    });

    it('is a different text when the post is handed a different one', async () => {
      await open('before');
      await type('typed but not saved');

      fixture.componentRef.setInput('content', 'someone changed it elsewhere');
      await settle();

      expect(box().value).toBe('someone changed it elsewhere');
    });

    it('keeps what was typed when other things change around it', async () => {
      await open('before');
      await type('typed but not saved');

      fixture.componentRef.setInput('error', 'Could not save');
      await settle();

      expect(box().value).toBe('typed but not saved');
    });
  });

  describe('Save', () => {
    it('is off until the text is different from the post', async () => {
      await open('Hello');
      expect(saveButton().disabled).toBe(true);

      await type('Hello!');
      expect(saveButton().disabled).toBe(false);

      await type('Hello');
      expect(saveButton().disabled).toBe(true);
    });

    it('is off for nothing but spaces', async () => {
      await open('Hello');

      await type('   \n ');

      expect(saveButton().disabled).toBe(true);
    });

    it('does not count spaces around the text as a change', async () => {
      await open('Hello');

      await type('  Hello \n');

      expect(saveButton().disabled).toBe(true);
    });

    it('sends the new text without the spaces around it', async () => {
      await open('Hello');
      await type('  Hello again \n');

      saveButton().click();

      expect(submitted).toEqual(['Hello again']);
      expect(cancelled).toBe(0);
    });

    it('does nothing when the text has not changed', async () => {
      await open('Hello');

      saveButton().click();

      expect(submitted).toEqual([]);
    });

    it('is saved with Ctrl+Enter or Cmd+Enter, when there is something to save', async () => {
      await open('Hello');
      press({ key: 'Enter', ctrlKey: true });
      expect(submitted).toEqual([]); // nothing changed yet

      await type('Hello 1');
      press({ key: 'Enter', ctrlKey: true });
      expect(submitted).toEqual(['Hello 1']);
      press({ key: 'Enter', metaKey: true });
      expect(submitted).toEqual(['Hello 1', 'Hello 1']);
    });

    it('is not saved by a plain Enter or Shift+Enter, which are new lines', async () => {
      await open('Hello');
      await type('Hello 1');

      press({ key: 'Enter' });
      press({ key: 'Enter', shiftKey: true });

      expect(submitted).toEqual([]);
    });
  });

  describe('Cancel', () => {
    it('says so, without sending anything', async () => {
      await open('Hello');
      await type('Changed my mind');

      cancelButton().click();

      expect(cancelled).toBe(1);
      expect(submitted).toEqual([]);
    });

    it('is also Escape', async () => {
      await open('Hello');

      press({ key: 'Escape' });

      expect(cancelled).toBe(1);
    });
  });

  describe('while saving', () => {
    it('locks the box and both buttons, and says what is going on', async () => {
      await open('Hello', { saving: true });
      await type('Hello!');

      expect(box().disabled).toBe(true);
      expect(saveButton().disabled).toBe(true);
      expect(cancelButton().disabled).toBe(true);
      expect(saveButton().textContent?.trim()).toBe('Saving...');
    });

    it('ignores Escape and Ctrl+Enter', async () => {
      await open('Hello', { saving: true });
      await type('Hello!');

      press({ key: 'Escape' });
      press({ key: 'Enter', ctrlKey: true });

      expect(cancelled).toBe(0);
      expect(submitted).toEqual([]);
    });

    it('says Save otherwise', async () => {
      await open('Hello');

      expect(saveButton().textContent?.trim()).toBe('Save');
    });
  });

  describe('a failure', () => {
    it('is shown as an alert, and the text stays', async () => {
      await open('Hello', { error: 'Could not save your changes. Please try again.' });

      const alert = el().querySelector('[role="alert"]');
      expect(alert?.textContent).toBe('Could not save your changes. Please try again.');
    });

    it('shows nothing when there is none', async () => {
      await open('Hello');

      expect(el().querySelector('[role="alert"]')).toBeNull();
    });
  });

  it('does not let a click inside it reach the post, which would open the thread', async () => {
    await open('Hello');
    let reached = false;
    el().addEventListener('click', () => (reached = true));

    box().click();
    cancelButton().click();

    expect(reached).toBe(false);
  });
});
