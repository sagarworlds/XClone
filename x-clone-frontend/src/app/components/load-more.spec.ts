import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadMoreComponent } from './load-more';

describe('LoadMoreComponent', () => {
  let fixture: ComponentFixture<LoadMoreComponent>;
  let source: { hasMore: ReturnType<typeof signal<boolean>>; loadingMore: ReturnType<typeof signal<boolean>>; failed: ReturnType<typeof signal<boolean>>; loadMore: ReturnType<typeof vi.fn> };

  const button = () => fixture.nativeElement.querySelector('button') as HTMLButtonElement | null;
  const errorText = () => fixture.nativeElement.querySelector('.load-more-error')?.textContent?.trim() ?? null;
  const render = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
  };

  beforeEach(async () => {
    source = { hasMore: signal(true), loadingMore: signal(false), failed: signal(false), loadMore: vi.fn() };
    await TestBed.configureTestingModule({ imports: [LoadMoreComponent] }).compileComponents();
    fixture = TestBed.createComponent(LoadMoreComponent);
    fixture.componentRef.setInput('list', source);
  });

  it('shows a Load more button while there is more', async () => {
    await render();

    expect(button()?.textContent?.trim()).toBe('Load more');
    expect(button()?.disabled).toBe(false);
    expect(errorText()).toBeNull();
  });

  it('shows nothing at all once everything is loaded', async () => {
    source.hasMore.set(false);

    await render();

    expect(button()).toBeNull();
  });

  it('asks the list for the next page when clicked', async () => {
    await render();

    button()!.click();

    expect(source.loadMore).toHaveBeenCalledTimes(1);
  });

  it('is disabled and says so while a page is loading', async () => {
    source.loadingMore.set(true);

    await render();

    expect(button()?.textContent?.trim()).toBe('Loading...');
    expect(button()?.disabled).toBe(true);
    button()!.click(); // a disabled button does not fire click
    expect(source.loadMore).not.toHaveBeenCalled();
  });

  it('explains a failure and offers to try again', async () => {
    source.failed.set(true);

    await render();

    expect(errorText()).toContain("Couldn't load more");
    expect(button()?.textContent?.trim()).toBe('Try again');
    button()!.click();
    expect(source.loadMore).toHaveBeenCalledTimes(1);
  });

  it('follows the list as it changes', async () => {
    await render();

    source.loadingMore.set(true);
    await render();
    expect(button()?.textContent?.trim()).toBe('Loading...');

    source.loadingMore.set(false);
    source.hasMore.set(false);
    await render();
    expect(button()).toBeNull();
  });
});
