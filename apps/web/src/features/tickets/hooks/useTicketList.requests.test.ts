import { act, renderHook, cleanup } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

afterEach(() => { cleanup(); vi.useRealTimers(); vi.unstubAllGlobals(); });

it('sends a status-only change through the real query serializer immediately', async () => {
  vi.useFakeTimers();
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(page([ticketListItem()]))));
  vi.stubGlobal('fetch', fetch);
  const { result } = renderHook(() => useTicketList());
  await act(async () => { await vi.advanceTimersByTimeAsync(300); });
  fetch.mockClear();
  await act(async () => { result.current.updateFilters({ status: 'InProgress' }); });
  expect(fetch).toHaveBeenCalledTimes(1);
  const url = new URL(String(fetch.mock.calls[0][0]), 'http://localhost');
  expect(url.searchParams.get('status')).toBe('InProgress');
});
