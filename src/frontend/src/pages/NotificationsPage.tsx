import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { NotificationItem } from '../types';
import { formatDateTime } from '../utils/format';

/** User Story 3009 - booking notifications and delivery receipts. */
export default function NotificationsPage() {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [reference, setReference] = useState('');
  const [error, setError] = useState('');

  const load = async (bookingReference?: string) => {
    try {
      setItems(await api.listNotifications(bookingReference));
      setError('');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <section className="card">
      <h2>Notifications</h2>
      <div className="row">
        <label>
          Booking reference
          <input value={reference} onChange={(e) => setReference(e.target.value)} placeholder="RTS-XXXXXXXX" />
        </label>
        <button type="button" onClick={() => void load(reference || undefined)}>
          Filter
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <table>
        <thead>
          <tr>
            <th>Sent</th>
            <th>Booking</th>
            <th>Type</th>
            <th>Channel</th>
            <th>Recipient</th>
            <th>Subject</th>
            <th>Delivered</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id}>
              <td>{formatDateTime(item.sentUtc)}</td>
              <td>{item.bookingReference}</td>
              <td>{item.type}</td>
              <td>{item.channel}</td>
              <td>{item.recipient}</td>
              <td>{item.subject}</td>
              <td>{item.delivered ? 'Yes' : 'No'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
