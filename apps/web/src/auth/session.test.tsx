import { safeReturnPath } from './useSignIn';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import type { Session } from './session';
import { ProtectedApp } from './ProtectedApp';
import { getSession, setSession } from './session';
import { request } from '../api/client';
afterEach(() => {
  cleanup();
  setSession(null);
  vi.unstubAllGlobals();
});
it('redirects an unauthenticated visitor to login', async () => {
  render(
    <MemoryRouter initialEntries={['/tickets/42']}>
      <Routes>
        <Route path="/tickets/:id" element={<ProtectedApp />} />
        <Route path="/login" element={<p>Sign in required</p>} />
      </Routes>
    </MemoryRouter>,
  );
  expect(await screen.findByText('Sign in required')).toBeInTheDocument();
});
it('attaches the token centrally and clears it on 401', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response('{}', { status: 401 }));
  vi.stubGlobal('fetch', fetch);
  setSession({
    token: 'fixture',
    expiresAtUtc: '2026-10-19T12:00:00Z',
    previousSuccessfulLoginAtUtc: null,
    user: {
      id: 1,
      fullName: 'Alex',
      email: 'a@example.com',
      lastSuccessfulLoginAtUtc: null,
    },
  });
  await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });
  expect(fetch.mock.calls[0][1].headers.Authorization).toBe('Bearer fixture');
  expect(getSession()).toBeNull();
});

it.each([
  'https://evil.example',
  '//evil.example',
  '/\\evil.example',
  '/login',
])('rejects unsafe return destination %s', (value) => {
  expect(safeReturnPath(value)).toBe('/tickets');
});
it('preserves a local ticket destination', () =>
  expect(safeReturnPath('/tickets/42?tab=history')).toBe(
    '/tickets/42?tab=history',
  ));

const fixtureSession = (token: string): Session => ({
  token,
  expiresAtUtc: '2026-10-20T12:00:00Z',
  previousSuccessfulLoginAtUtc: null,
  user: { id: 1, fullName: 'Alex', email: 'a@example.com', lastSuccessfulLoginAtUtc: null },
});

it.each([200, 401])('does not let an old %s response replace a newer session', async (status) => {
  let finish!: (response: Response) => void;
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { finish = resolve; })));
  setSession(fixtureSession('old-token'));
  const pending = request('/tickets');
  const newer = fixtureSession('new-token');
  setSession(newer);
  finish(new Response('{}', { status }));

  await expect(pending).rejects.toMatchObject({ status: 401 });
  expect(getSession()).toBe(newer);
});

it('returns to the requested ticket after a successful login', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(fixtureSession('valid-token')))));
  render(
    <MemoryRouter initialEntries={[{ pathname: '/login', state: { returnTo: '/tickets/42' } }]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/tickets/42" element={<p>Requested ticket 42</p>} />
      </Routes>
    </MemoryRouter>,
  );
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@example.com' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'fixture-password' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

  expect(await screen.findByText('Requested ticket 42')).toBeInTheDocument();
});
