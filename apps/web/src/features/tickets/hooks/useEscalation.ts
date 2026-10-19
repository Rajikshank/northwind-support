import { useCallback, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { toErrorMessage } from '../../../api/client';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketDetail } from '../../../types/api';

export function useEscalation(id: number, onUpdated: (ticket: TicketDetail) => void) {
  const load = useCallback(() => ticketsApi.getEscalations(id), [id]);
  const history = useAsyncData(load, 'Unable to load escalation history.');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const submit = async (reason: string) => {
    setPending(true); setError(undefined);
    try {
      const result = await ticketsApi.escalate(id, reason);
      onUpdated(result.ticket);
      await history.reload();
      return true;
    } catch (caught) {
      setError(toErrorMessage(caught, 'Unable to escalate ticket.'));
      return false;
    } finally { setPending(false); }
  };
  return { history, pending, error, submit };
}
