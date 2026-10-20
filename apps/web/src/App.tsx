import { setSession, useSession } from './auth/session';
import { Link, NavLink, Outlet } from 'react-router-dom';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <div className="app__session">
          <span>{session?.user.fullName}</span>{' '}
          <button
            className="button button--ghost"
            onClick={() => setSession(null)}
          >
            Sign out
          </button>
          <p className="summary__note">
            Previous successful sign-in:{' '}
            {session?.previousSuccessfulLoginAtUtc
              ? new Date(session.previousSuccessfulLoginAtUtc).toLocaleString()
              : 'First sign-in'}
          </p>
        </div>
        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
