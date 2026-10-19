import { act, renderHook, cleanup } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import type { TicketFilters } from '../../../types/api';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

it('sends a status-only change through the real query serializer immediately', async () => {
  vi.useFakeTimers();
  const fetch = vi
    .fn()
    .mockResolvedValue(new Response(JSON.stringify(page([ticketListItem()]))));
  vi.stubGlobal('fetch', fetch);
  const { result } = renderHook(() => useTicketList());
  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });
  fetch.mockClear();
  await act(async () => {
    result.current.updateFilters({ status: 'InProgress' });
  });
  expect(fetch).toHaveBeenCalledTimes(1);
  const url = new URL(String(fetch.mock.calls[0][0]), 'http://localhost');
  expect(url.searchParams.get('status')).toBe('InProgress');
});

it.each<[Partial<TicketFilters>, string, string]>([
  [{ priority: 'High' }, 'priority', 'High'],
  [{ categoryId: 2 }, 'categoryId', '2'],
  [{ customerId: 3 }, 'customerId', '3'],
  [{ assignedAgentId: 4 }, 'assignedAgentId', '4'],
  [{ unassignedOnly: true }, 'unassignedOnly', 'true'],
  [{ slaStatus: 'AtRisk' }, 'slaStatus', 'AtRisk'],
  [{ page: 2 }, 'page', '2'],
  [{ sortBy: 'priority' }, 'sortBy', 'priority'],
])('sends immediate query patch %j', async (patch, key, value) => {
  const fetch = vi
    .fn()
    .mockImplementation(() =>
      Promise.resolve(new Response(JSON.stringify(page([])))),
    );
  vi.stubGlobal('fetch', fetch);
  const { result } = renderHook(() => useTicketList());
  await act(async () => {});
  fetch.mockClear();
  await act(async () => {
    result.current.updateFilters(patch);
  });
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(
    new URL(
      String(fetch.mock.calls[0][0]),
      'http://localhost',
    ).searchParams.get(key),
  ).toBe(value);
});

it('does not reset page or request until search settles, and reset cancels pending search', async () => {
  vi.useFakeTimers();
  const fetch = vi
    .fn()
    .mockImplementation(() =>
      Promise.resolve(new Response(JSON.stringify(page([])))),
    );
  vi.stubGlobal('fetch', fetch);
  const { result } = renderHook(() => useTicketList());
  await act(async () => {
    result.current.updateFilters({ page: 3 });
  });
  fetch.mockClear();
  act(() => result.current.updateFilters({ search: 'p' }));
  act(() => result.current.updateFilters({ search: 'portal' }));
  expect(fetch).not.toHaveBeenCalled();
  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });
  expect(fetch).toHaveBeenCalledTimes(1);
  const url = new URL(String(fetch.mock.calls[0][0]), 'http://localhost');
  expect(url.searchParams.get('search')).toBe('portal');
  expect(url.searchParams.get('page')).toBe('1');
  act(() => result.current.updateFilters({ search: 'pending' }));
  await act(async () => result.current.resetFilters());
  fetch.mockClear();
  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });
  expect(fetch).not.toHaveBeenCalled();
});

it('ignores an older response arriving after the newest query', async () => {
  let finishOld!: (response: Response) => void;
  const fetch = vi
    .fn()
    .mockImplementationOnce(
      () =>
        new Promise<Response>((resolve) => {
          finishOld = resolve;
        }),
    )
    .mockResolvedValueOnce(
      new Response(JSON.stringify(page([ticketListItem({ id: 99 })]))),
    );
  vi.stubGlobal('fetch', fetch);
  const { result } = renderHook(() => useTicketList());
  await act(async () => result.current.updateFilters({ priority: 'Low' }));
  await act(async () =>
    finishOld(new Response(JSON.stringify(page([ticketListItem({ id: 1 })])))),
  );
  expect(result.current.data?.items[0].id).toBe(99);
});
