import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns immediate filters separately from search input's debounce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [search, setSearch] = useState('');
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState<State>({ isLoading: true });
  const pendingSearch = useRef<number | undefined>(undefined);

  useEffect(() => {
    const controller = new AbortController();
    // Some transports can finish after cancellation; never publish their stale result.
    let current = true;
    const load = async () => {
      setState((previous) => ({ ...previous, isLoading: true, error: undefined }));
      try {
        const data = await ticketsApi.getTickets(filters, controller.signal);
        if (current) setState({ isLoading: false, data });
      } catch (error) {
        if (current) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((previous) => ({ ...previous, isLoading: false, error: message }));
        }
      }
    };
    void load();
    return () => { current = false; controller.abort(); };
  }, [filters, revision]);

  useEffect(() => () => window.clearTimeout(pendingSearch.current), []);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    if (patch.search !== undefined) {
      const value = patch.search;
      setSearch(value);
      window.clearTimeout(pendingSearch.current);
      // Commit search and its page reset together, so typing on page 2 sends no early request.
      pendingSearch.current = window.setTimeout(() => {
        setFilters((previous) => ({ ...previous, search: value, page: 1 }));
      }, DEBOUNCE_MS);
    }
    const { search: ignoredSearch, ...immediate } = patch;
    void ignoredSearch;
    if (Object.keys(immediate).length) {
      setFilters((previous) => ({ ...previous, ...immediate, page: patch.page ?? 1 }));
    }
  };
  const resetFilters = () => {
    window.clearTimeout(pendingSearch.current);
    setSearch('');
    setFilters({ ...emptyFilters });
  };
  return {
    filters: { ...filters, search }, updateFilters, resetFilters,
    reload: () => setRevision((value) => value + 1), ...state,
  };
}
