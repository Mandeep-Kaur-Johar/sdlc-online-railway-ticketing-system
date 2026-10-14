import { NavLink, Navigate, Route, Routes } from 'react-router-dom';
import SearchPage from './pages/SearchPage';
import BookingsPage from './pages/BookingsPage';
import ProfilesPage from './pages/ProfilesPage';
import NotificationsPage from './pages/NotificationsPage';
import SupportPage from './pages/SupportPage';
import { CURRENT_USER_ID } from './session';

export default function App() {
  return (
    <div className="app">
      <header className="app-header">
        <h1>Online Railway Ticketing</h1>
        <nav>
          <NavLink to="/search">Search &amp; Book</NavLink>
          <NavLink to="/bookings">My Bookings</NavLink>
          <NavLink to="/passengers">Passengers</NavLink>
          <NavLink to="/notifications">Notifications</NavLink>
          <NavLink to="/support">Support</NavLink>
        </nav>
        <span className="user-badge">Signed in as {CURRENT_USER_ID}</span>
      </header>

      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/search" replace />} />
          <Route path="/search" element={<SearchPage />} />
          <Route path="/bookings" element={<BookingsPage />} />
          <Route path="/passengers" element={<ProfilesPage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/support" element={<SupportPage />} />
          <Route path="*" element={<Navigate to="/search" replace />} />
        </Routes>
      </main>

      <footer className="app-footer">Epic 2998 - Online Railway Ticketing System</footer>
    </div>
  );
}
