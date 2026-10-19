import { Navigate, useLocation } from 'react-router-dom';
import { useSession } from './session';
import { App } from '../App';
export function ProtectedApp() {
  const session = useSession();
  const location = useLocation();
  return session ? <App /> : <Navigate to="/login" state={{returnTo: location.pathname + location.search}} replace />;
}
