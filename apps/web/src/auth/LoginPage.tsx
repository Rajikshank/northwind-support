import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { safeReturnPath, useSignIn } from './useSignIn';
import { useSession } from './session';
export function LoginPage() {
  const session = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const candidate: unknown = location.state;
  const requested =
    candidate && typeof candidate === 'object' && 'returnTo' in candidate
      ? candidate.returnTo
      : '/tickets';
  const returnTo = safeReturnPath(requested);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const { pending, error, signIn } = useSignIn();
  if (session) return <Navigate to={returnTo} replace />;
  return (
    <main className="app__main">
      <section className="card" style={{ maxWidth: 480, margin: '3rem auto' }}>
        <h1>Sign in to Northwind Support</h1>
        <form
          onSubmit={async (event) => {
            event.preventDefault();
            if (await signIn(email, password)) {
              setPassword('');
              await navigate(returnTo, { replace: true });
            }
          }}
        >
          <div className="field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              type="email"
              autoComplete="username"
              required
              maxLength={256}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={1024}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>
          {error && <p role="alert">{error}</p>}
          <button className="button button--primary" disabled={pending}>
            {pending ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
