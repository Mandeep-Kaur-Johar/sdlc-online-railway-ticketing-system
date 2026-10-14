import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import { CURRENT_USER_ID } from '../session';
import type { Booking, RefundQuote } from '../types';
import { canCancel, canPay, formatCurrency, formatDateTime } from '../utils/format';

/** User Stories 3006 and 3007 - pay for a held booking, cancel and see the refund. */
export default function BookingsPage() {
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [quotes, setQuotes] = useState<Record<string, RefundQuote>>({});
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setBusy(true);
    try {
      setBookings(await api.listBookings(CURRENT_USER_ID));
      setError('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const quote = async (reference: string) => {
    try {
      const result = await api.refundQuote(reference);
      setQuotes((current) => ({ ...current, [reference]: result }));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const cancel = async (reference: string) => {
    setBusy(true);
    try {
      const cancelled = await api.cancelBooking(reference, CURRENT_USER_ID, 'Cancelled by passenger');
      setMessage(
        `Booking ${cancelled.bookingReference} cancelled. Refund ${formatCurrency(cancelled.refundAmount ?? 0)} (${cancelled.refundPercentage ?? 0}%).`
      );
      await load();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const pay = async (reference: string) => {
    setBusy(true);
    try {
      const intent = await api.createPaymentIntent(reference, 'Card');
      setMessage(`Payment intent ${intent.providerIntentId} created for ${formatCurrency(intent.amount)}.`);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section>
      <div className="card">
        <h2>My bookings</h2>
        <button type="button" className="secondary" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
        {error ? <p className="error">{error}</p> : null}
        {message ? <p className="success">{message}</p> : null}
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Train</th>
              <th>Departure</th>
              <th>Class</th>
              <th>Seats</th>
              <th>Total</th>
              <th>Status</th>
              <th>Payment</th>
              <th>Ticket</th>
              <th>Refund</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {bookings.map((booking) => (
              <tr key={booking.bookingReference}>
                <td>{booking.bookingReference}</td>
                <td>
                  {booking.trainNumber} {booking.trainName}
                </td>
                <td>{formatDateTime(booking.departureUtc)}</td>
                <td>{booking.travelClass}</td>
                <td>{booking.seatCount}</td>
                <td>{formatCurrency(booking.totalFare)}</td>
                <td>{booking.status}</td>
                <td>{booking.paymentStatus}</td>
                <td>{booking.ticketNumber ?? '-'}</td>
                <td>
                  {booking.refundAmount != null
                    ? `${formatCurrency(booking.refundAmount)} (${booking.refundPercentage ?? 0}%)`
                    : quotes[booking.bookingReference]
                      ? `Quote ${formatCurrency(quotes[booking.bookingReference].amount)} (${quotes[booking.bookingReference].percentage}%)`
                      : '-'}
                </td>
                <td>
                  {canPay(booking) ? (
                    <button type="button" onClick={() => void pay(booking.bookingReference)} disabled={busy}>
                      Pay
                    </button>
                  ) : null}
                  {canCancel(booking) ? (
                    <>
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => void quote(booking.bookingReference)}
                        disabled={busy}
                      >
                        Refund quote
                      </button>
                      <button type="button" onClick={() => void cancel(booking.bookingReference)} disabled={busy}>
                        Cancel
                      </button>
                    </>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
