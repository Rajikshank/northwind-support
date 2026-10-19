import { useSyncExternalStore } from 'react';
export interface AuthUser { id: number; fullName: string; email: string; lastSuccessfulLoginAtUtc: string | null }
export interface Session { token: string; expiresAtUtc: string; user: AuthUser; previousSuccessfulLoginAtUtc: string | null }
let session: Session | null = null;
const listeners = new Set<() => void>();
export const getSession = () => session;
export function setSession(value: Session | null) {
  session = value;
  listeners.forEach(listener => listener());
}
export function useSession() {
  return useSyncExternalStore(listener => { listeners.add(listener); return () => { listeners.delete(listener); }; }, getSession);
}
