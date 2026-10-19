import { fireEvent, render, screen, waitFor, cleanup } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { EscalationPanel } from './EscalationPanel';
import { SlaCell } from './SlaCell';
import { ticketsApi } from '../../../api/tickets';
import { ApiError } from '../../../api/client';
import { ticketListItem } from '../../../test/fixtures';
import type { TicketDetail, SlaStatus } from '../../../types/api';
vi.mock('../../../api/tickets', () => ({ticketsApi: {getEscalations: vi.fn(), escalate: vi.fn()}}));
const ticket: TicketDetail = {...ticketListItem(), description: 'Details', customer: {...ticketListItem().customer, email: 'test@example.com', phone: null}, category: {...ticketListItem().category, requiresSpecialist: false}};
beforeEach(() => vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
it('blocks a short reason without sending it', async () => {
  render(<EscalationPanel ticket={ticket} agents={[]} onUpdated={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Reason'), {target: {value: 'bad'}});
  fireEvent.click(screen.getByText('Escalate ticket'));
  expect(await screen.findByRole('alert')).toHaveTextContent('5-500');
  expect(ticketsApi.escalate).not.toHaveBeenCalled();
});
it('shows conflict without replacing the ticket', async () => {
  vi.mocked(ticketsApi.escalate).mockRejectedValue(new ApiError(409, 'Critical tickets cannot be escalated further.'));
  const updated = vi.fn();
  render(<EscalationPanel ticket={ticket} agents={[]} onUpdated={updated} />);
  fireEvent.change(screen.getByLabelText('Reason'), {target: {value: 'Customer site is down'}});
  fireEvent.click(screen.getByText('Escalate ticket'));
  await waitFor(() => expect(screen.getByText('Critical tickets cannot be escalated further.')).toBeInTheDocument());
  expect(updated).not.toHaveBeenCalled();
});
it.each<[SlaStatus, string]>([['WithinSla', 'Within SLA'], ['AtRisk','At risk'], ['Breached','Breached'], ['Met','Met'], ['NotApplicable','No SLA']])('labels %s in text', (status, label) => {
  render(<SlaCell dueAtUtc={null} slaStatus={status} />);
  expect(screen.getByText(label)).toBeInTheDocument();
});
