// Frontend browser check with deterministic HTTP fixtures. Not a SQL Server end-to-end test.
import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
const output = process.env.EVIDENCE_DIR ?? 'docs/evidence';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const errors = [];
page.on('pageerror', (error) => errors.push(error.message));
const now = new Date().toISOString();
const initialDue = new Date(Date.now() + 30 * 60000).toISOString();
const escalatedDue = new Date(Date.now() + 2 * 3600000).toISOString();
const agent = {
  id: 1,
  fullName: 'Alex Turner',
  email: 'alex@example.com',
  isActive: true,
  maxOpenTickets: 10,
  openTicketCount: 2,
  specializations: [],
};
let ticket = {
  id: 1,
  reference: 'TCK-0001',
  title: 'Portal unavailable',
  description: 'Users cannot access the portal.',
  status: 'Open',
  priority: 'High',
  customer: {
    id: 1,
    name: 'Contoso',
    tier: 'Premium',
    email: 'support@example.com',
    phone: null,
  },
  category: { id: 1, name: 'General', requiresSpecialist: false },
  assignedAgent: agent,
  createdAtUtc: now,
  updatedAtUtc: now,
  dueAtUtc: initialDue,
  resolvedAtUtc: null,
  slaStatus: 'AtRisk',
};
let history = [];
const queries = [];
await page.route('http://127.0.0.1:5173/api/**', async (route) => {
  const url = new URL(route.request().url());
  const path = url.pathname;
  let body;
  if (path === '/api/auth/login')
    body = {
      token: 'browser-fixture',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      user: agent,
      previousSuccessfulLoginAtUtc: null,
    };
  else if (path === '/api/agents') body = [agent];
  else if (path === '/api/customers')
    body = [{ ...ticket.customer, openTicketCount: 1 }];
  else if (path === '/api/categories')
    body = [
      { ...ticket.category, isActive: true, forcesCriticalPriority: false },
    ];
  else if (path === '/api/tickets/1/escalations') body = history;
  else if (path === '/api/tickets/1/escalate') {
    ticket = {
      ...ticket,
      priority: 'Critical',
      slaStatus: 'WithinSla',
      dueAtUtc: escalatedDue,
    };
    history = [
      {
        id: 1,
        ticketId: 1,
        fromPriority: 'High',
        toPriority: 'Critical',
        fromAgentId: 1,
        toAgentId: 1,
        fromDueAtUtc: initialDue,
        toDueAtUtc: escalatedDue,
        reason: route.request().postDataJSON().reason,
        escalatedBy: 'agent:1',
        escalatedAtUtc: now,
      },
    ];
    body = { ticket, escalation: history[0] };
  } else if (path === '/api/tickets/1') body = ticket;
  else if (path === '/api/tickets' && route.request().method() === 'POST') {
    body = {
      ...ticket,
      id: 2,
      reference: 'TCK-0002',
      priority: 'Low',
      assignedAgent: null,
      triage: {
        appliedPriority: 'Low',
        priorityReason: 'Requested Low priority respected.',
        slaWindowHours: 36,
        slaReason: 'Premium window: 36 hours.',
        assignmentReason: 'No active eligible agent has available capacity.',
      },
    };
  } else if (path === '/api/tickets') {
    queries.push(url.search);
    body = {
      items: [ticket],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };
  } else return route.fulfill({ status: 404, body: '{}' });
  return route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
});
try {
  await page.goto('http://127.0.0.1:5173/tickets');
  await page.getByLabel('Email', { exact: true }).waitFor();
  await page.screenshot({
    path: `${output}/login-desktop.png`,
    fullPage: true,
  });
  await page.getByLabel('Email', { exact: true }).fill('alex@example.com');
  await page
    .getByLabel('Password', { exact: true })
    .fill('LocalFixture-Password!');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'TCK-0001' }).waitFor();
  const previous = queries.length;
  await page.getByLabel('Status', { exact: true }).selectOption('InProgress');
  await page.waitForFunction(
    () => document.querySelector('#filter-status')?.value === 'InProgress',
  );
  await page.waitForTimeout(150);
  assert(
    queries
      .slice(previous)
      .some(
        (query) => new URLSearchParams(query).get('status') === 'InProgress',
      ),
  );
  await page.screenshot({
    path: `${output}/tickets-desktop.png`,
    fullPage: true,
  });
  await page.getByRole('link', { name: 'TCK-0001' }).click();
  await page.getByLabel('Reason', { exact: true }).waitFor();
  await page.screenshot({
    path: `${output}/escalation-form-desktop.png`,
    fullPage: true,
  });
  await page
    .getByLabel('Reason', { exact: true })
    .fill('Customer site is unavailable');
  await page.getByRole('button', { name: 'Escalate ticket' }).click();
  await page.getByText('High to Critical', { exact: true }).waitFor();
  await page.screenshot({
    path: `${output}/escalation-desktop.png`,
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: `${output}/escalation-mobile.png`,
    fullPage: true,
  });
  assert(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  );
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page
    .getByRole('link', { name: 'Tickets', exact: true })
    .first()
    .click();
  await page.getByRole('link', { name: 'New ticket', exact: true }).click();
  await page.getByLabel('Title', { exact: true }).fill('Another portal issue');
  await page
    .getByLabel('Description', { exact: true })
    .fill('Another customer cannot sign in.');
  await page.getByLabel('Customer', { exact: true }).selectOption('1');
  await page.getByLabel('Category', { exact: true }).selectOption('1');
  await page.getByRole('button', { name: 'Raise ticket', exact: true }).click();
  await page
    .getByText('No active eligible agent has available capacity.', {
      exact: false,
    })
    .waitFor();
  await page.screenshot({
    path: `${output}/triage-result-desktop.png`,
    fullPage: true,
  });
  assert.deepEqual(errors, []);
  console.log(
    JSON.stringify(
      {
        result: 'PASS',
        fixtureBackend: true,
        checks: [
          'login redirect',
          'login',
          'status request',
          'escalation/history refresh',
          'mobile overflow',
          'no browser errors',
        ],
        screenshots: 6,
      },
      null,
      2,
    ),
  );
} finally {
  await browser.close();
}
