import { useState } from 'react';
import { request, toErrorMessage } from '../api/client';
import { setSession, type Session } from './session';

export function useSignIn() {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const signIn = async (email: string, password: string) => {
    if (!email.trim() || !password) {
      setError('Enter your email and password.');
      return false;
    }
    setPending(true);
    setError(undefined);
    try {
      const result = await request<Session>('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim(), password }),
      });
      setSession(result);
      return true;
    } catch (caught) {
      setError(toErrorMessage(caught, 'Unable to sign in.'));
      return false;
    } finally {
      setPending(false);
    }
  };
  return { pending, error, signIn };
}

/** Reject external destinations, including browser-normalized backslash URLs. */
export function safeReturnPath(value: unknown): string {
  if (typeof value !== 'string' || !value.startsWith('/')) return '/tickets';
  const url = new URL(value, window.location.origin);
  if (
    url.origin !== window.location.origin ||
    url.pathname.startsWith('/login')
  )
    return '/tickets';
  return url.pathname + url.search + url.hash;
}
