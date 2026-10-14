import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import { CURRENT_USER_ID } from '../session';
import type { SupportRequest } from '../types';
import { formatDateTime } from '../utils/format';

/** User Story 3010 - raise support requests and track them to closure. */
export default function SupportPage() {
  const [requests, setRequests] = useState<SupportRequest[]>([]);
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [bookingReference, setBookingReference] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    try {
      setRequests(await api.listSupportRequests(CURRENT_USER_ID));
    } catch (err) {
      setError((err as Error).message);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError('');
    try {
      const created = await api.createSupportRequest({
        userId: CURRENT_USER_ID,
        bookingReference: bookingReference.trim() || null,
        subject,
        description
      });
      setMessage(`Support request ${created.reference} logged. First response within 8 business hours.`);
      setSubject('');
      setDescription('');
      setBookingReference('');
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <section>
      <form className="card" onSubmit={submit}>
        <h2>Contact support</h2>
        <div className="row">
          <label>
            Booking reference (optional)
            <input value={bookingReference} onChange={(e) => setBookingReference(e.target.value)} placeholder="RTS-XXXXXXXX" />
          </label>
          <label>
            Subject
            <input value={subject} onChange={(e) => setSubject(e.target.value)} required />
          </label>
        </div>
        <label>
          Description
          <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} required />
        </label>
        <button type="submit">Raise request</button>
        {error ? <p className="error">{error}</p> : null}
        {message ? <p className="success">{message}</p> : null}
      </form>

      <div className="card">
        <h2>My support requests</h2>
        {requests.map((request) => (
          <article key={request.reference}>
            <h3>
              {request.reference} - {request.subject} <span className="badge available">{request.status}</span>
            </h3>
            <p>{request.description}</p>
            <p>
              Raised {formatDateTime(request.createdUtc)} | Last updated {formatDateTime(request.updatedUtc)}
              {request.bookingReference ? ` | Booking ${request.bookingReference}` : ''}
            </p>
            <ul>
              {request.updates.map((update, index) => (
                <li key={index}>
                  {formatDateTime(update.createdUtc)} - {update.status} - {update.note} ({update.author})
                </li>
              ))}
            </ul>
            {request.resolution ? <p className="success">Resolution: {request.resolution}</p> : null}
          </article>
        ))}
      </div>
    </section>
  );
}
