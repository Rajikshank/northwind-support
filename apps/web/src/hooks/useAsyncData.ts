import { useCallback, useEffect, useRef, useState } from 'react';
import { toErrorMessage } from '../api/client';

interface AsyncState<T> {
  isLoading: boolean;
  data?: T;
  error?: string;
}

export interface AsyncData<T> extends AsyncState<T> {
  /** Runs the loader again, for a retry button or after a mutation. */
  reload: () => Promise<void>;
  /** Replaces the data without a round trip, e.g. with the response of a PATCH. */
  setData: (value: T) => void;
}

/**
 * Loads data when the given loader changes, and tracks loading and error state.
 *
 * Pass a loader with a stable identity (a module-level function, or one wrapped in
 * `useCallback`), otherwise this will fetch on every render.
 */
export function useAsyncData<T>(
  load: () => Promise<T>,
  errorMessage: string,
): AsyncData<T> {
  const latestRequest = useRef(0);
  const currentLoader = useRef(load);
  const invalidate = useCallback(() => {
    latestRequest.current++;
  }, []);
  const [state, setState] = useState<AsyncState<T>>({ isLoading: true });

  const run = useCallback(async () => {
    if (currentLoader.current !== load) return;
    const requestId = ++latestRequest.current;
    setState({ isLoading: true });

    try {
      const data = await load();
      // Detail/history requests can finish in a different order from navigation.
      if (requestId === latestRequest.current) {
        setState({ isLoading: false, data });
      }
    } catch (error) {
      if (requestId !== latestRequest.current) return;
      setState((current) => ({
        ...current,
        isLoading: false,
        error: toErrorMessage(error, errorMessage),
      }));
    }
  }, [load, errorMessage]);

  useEffect(() => {
    currentLoader.current = load;
    void run();
    return invalidate;
  }, [load, run, invalidate]);

  const setData = useCallback((value: T) => {
    // A late mutation from the previous page must not replace the current ticket.
    if (currentLoader.current !== load) return;
    // A mutation response supersedes any read that was already in flight.
    invalidate();
    setState({ isLoading: false, data: value });
  }, [load, invalidate]);

  return { ...state, reload: run, setData };
}
