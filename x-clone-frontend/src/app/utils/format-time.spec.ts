import { formatTime } from './format-time';

describe('formatTime', () => {
  const now = Date.parse('2026-09-20T12:00:00Z');
  const ago = (ms: number) => new Date(now - ms).toISOString();
  const MINUTE = 60_000;
  const HOUR = 60 * MINUTE;

  it('says "Just now" for the first minute', () => {
    expect(formatTime(ago(0), now)).toBe('Just now');
    expect(formatTime(ago(59_000), now)).toBe('Just now');
  });

  it('counts minutes up to an hour', () => {
    expect(formatTime(ago(MINUTE), now)).toBe('1m');
    expect(formatTime(ago(5 * MINUTE + 30_000), now)).toBe('5m');
    expect(formatTime(ago(59 * MINUTE), now)).toBe('59m');
  });

  it('counts hours up to a day', () => {
    expect(formatTime(ago(HOUR), now)).toBe('1h');
    expect(formatTime(ago(23 * HOUR + 59 * MINUTE), now)).toBe('23h');
  });

  it('switches to a short date after a day', () => {
    const then = new Date(now - 24 * HOUR);

    expect(formatTime(then.toISOString(), now)).toBe(then.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
  });

  it('does not show negative times when the server clock is slightly ahead', () => {
    expect(formatTime(new Date(now + 5 * MINUTE).toISOString(), now)).toBe('Just now');
  });

  it('shows nothing for a value that is not a date', () => {
    expect(formatTime('', now)).toBe('');
    expect(formatTime('not a date', now)).toBe('');
  });

  it('uses the current time by default', () => {
    expect(formatTime(new Date().toISOString())).toBe('Just now');
  });
});
