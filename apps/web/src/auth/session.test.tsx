import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ProtectedApp } from './ProtectedApp';
import { getSession, setSession } from './session';
import { request } from '../api/client';
afterEach(() => {cleanup(); setSession(null); vi.unstubAllGlobals();});
it('redirects an unauthenticated visitor to login', async () => {
  render(<MemoryRouter initialEntries={['/tickets/42']}><Routes><Route path="/tickets/:id" element={<ProtectedApp />} /><Route path="/login" element={<p>Sign in required</p>} /></Routes></MemoryRouter>);
  expect(await screen.findByText('Sign in required')).toBeInTheDocument();
});
it('attaches the token centrally and clears it on 401', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response('{}', {status: 401})); vi.stubGlobal('fetch', fetch);
  setSession({token:'fixture', expiresAtUtc:'2026-10-19T12:00:00Z', previousSuccessfulLoginAtUtc:null, user:{id:1,fullName:'Alex',email:'a@example.com',lastSuccessfulLoginAtUtc:null}});
  await expect(request('/tickets')).rejects.toMatchObject({status:401});
  expect(fetch.mock.calls[0][1].headers.Authorization).toBe('Bearer fixture');
  expect(getSession()).toBeNull();
});
