/** How long ago something happened, the way timelines show it: "Just now", "5m", "3h", then a short date. */
export function formatTime(dateStr: string, now: number = Date.now()): string {
  const time = new Date(dateStr).getTime();
  if (Number.isNaN(time)) return '';

  const diffMins = Math.floor((now - time) / 60000);
  const diffHours = Math.floor(diffMins / 60);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m`;
  if (diffHours < 24) return `${diffHours}h`;
  return new Date(time).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}
