import { useState } from 'react';
import type { Agent, TicketDetail } from '../../../types/api';
import { formatDateTime } from '../../../lib/format';
import { useEscalation } from '../hooks/useEscalation';
import { ErrorMessage } from '../../../components/ErrorMessage';

export function EscalationPanel({ticket, agents, onUpdated}: {ticket: TicketDetail; agents: Agent[]; onUpdated: (ticket: TicketDetail) => void}) {
  const { history, pending, error, submit } = useEscalation(ticket.id, onUpdated);
  const [reason, setReason] = useState('');
  const [validation, setValidation] = useState<string>();
  const eligible = ticket.priority !== 'Critical' && ticket.status !== 'Resolved' && ticket.status !== 'Closed';
  const name = (id: number | null) => agents.find(a => a.id === id)?.fullName ?? (id === null ? 'Unassigned' : `Agent ${id}`);
  return <section className="card">
    <h2>Escalation</h2>
    {eligible ? <form onSubmit={async event => {
      event.preventDefault();
      if (reason.trim().length < 5 || reason.trim().length > 500) {
        setValidation('Enter a reason of 5-500 characters.'); return;
      }
      setValidation(undefined);
      if (await submit(reason.trim())) setReason('');
    }}>
      <label htmlFor="escalation-reason">Reason</label>
      <textarea id="escalation-reason" value={reason} onChange={e => setReason(e.target.value)} aria-invalid={!!validation} aria-describedby={validation ? 'escalation-validation' : undefined} />
      {validation && <p id="escalation-validation" role="alert">{validation}</p>}
      <button className="button button--primary" disabled={pending}>{pending ? 'Escalating...' : 'Escalate ticket'}</button>
    </form> : <p>This ticket cannot be escalated because it is {ticket.priority === 'Critical' ? 'Critical' : ticket.status}.</p>}
    {error && <ErrorMessage message={error} />}
    <h3>History</h3>
    {history.isLoading && <p role="status">Loading history...</p>}
    {history.error && <ErrorMessage message={history.error} onRetry={() => void history.reload()} />}
    {history.data?.length === 0 && <p>No escalations yet.</p>}
    {history.data?.map(item => <article key={item.id}>
      <p><strong>{item.fromPriority} to {item.toPriority}</strong></p>
      <p>Owner: {name(item.fromAgentId)} to {name(item.toAgentId)}</p>
      <p>Due: {formatDateTime(item.fromDueAtUtc)} to {formatDateTime(item.toDueAtUtc)}</p>
      <p>{item.reason}</p><p>{item.escalatedBy} - {formatDateTime(item.escalatedAtUtc)}</p>
    </article>)}
  </section>;
}
