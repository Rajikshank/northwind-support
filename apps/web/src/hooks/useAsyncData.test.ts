import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { useAsyncData } from './useAsyncData';

afterEach(cleanup);

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((complete) => { resolve = complete; });
  return { promise, resolve };
}

it('keeps the current ticket when an older load finishes last', async () => {
  const oldTicket = deferred<string>();
  const currentTicket = deferred<string>();
  const { result, rerender } = renderHook(
    ({ load }) => useAsyncData(load, 'Unable to load ticket'),
    { initialProps: { load: () => oldTicket.promise } },
  );
  rerender({ load: () => currentTicket.promise });

  await act(async () => currentTicket.resolve('Current ticket'));
  expect(result.current.data).toBe('Current ticket');
  await act(async () => oldTicket.resolve('Old ticket'));
  expect(result.current.data).toBe('Current ticket');
});

it('does not overwrite a mutation response with an earlier read', async () => {
  const reading = deferred<string>();
  const load = () => reading.promise;
  const { result } = renderHook(() => useAsyncData(load, 'Unable to load ticket'));
  act(() => result.current.setData('Escalated ticket'));
  await act(async () => reading.resolve('Ticket before escalation'));
  expect(result.current.data).toBe('Escalated ticket');
});

it('ignores a mutation callback belonging to a ticket left behind', async () => {
  const first = deferred<string>();
  const second = deferred<string>();
  const { result, rerender } = renderHook(
    ({ load }) => useAsyncData(load, 'Unable to load ticket'),
    { initialProps: { load: () => first.promise } },
  );
  const updateOldTicket = result.current.setData;
  rerender({ load: () => second.promise });
  await act(async () => second.resolve('Current ticket'));
  act(() => updateOldTicket('Old ticket after escalation'));
  expect(result.current.data).toBe('Current ticket');
});
