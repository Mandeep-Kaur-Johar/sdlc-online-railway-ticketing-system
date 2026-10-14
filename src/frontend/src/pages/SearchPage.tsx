import { useEffect, useState } from 'react';
import AvailabilityTable from '../components/AvailabilityTable';
import PassengerFields, { type PassengerInput } from '../components/PassengerFields';
import { api } from '../api/client';
import { CONTACT_DEFAULTS, CURRENT_USER_ID } from '../session';
import type { Booking, PassengerProfile, TrainSearchResult, TravelClass } from '../types';
import { formatCurrency, formatDateTime, holdCountdownMinutes, newIdempotencyKey } from '../utils/format';

const MAX_PASSENGERS = 6;

export default function SearchPage() {
  const [source, setSource] = useState('NDLS');
  const [destination, setDestination] = useState('BCT');
  const [date, setDate] = useState(new Date(Date.now() + 86400000).toISOString().slice(0, 10));
  const [results, setResults] = useState<TrainSearchResult[]>([]);
  const [selectedSchedule, setSelectedSchedule] = useState<TrainSearchResult | null>(null);
  const [selectedClass, setSelectedClass] = useState<TravelClass | null>(null);
  const [profiles, setProfiles] = useState<PassengerProfile[]>([]);
  const [passengers, setPassengers] = useState<PassengerInput[]>([
    { fullName: '', age: 30, gender: 'Female', passengerProfileId: null }
  ]);
  const [email, setEmail] = useState(CONTACT_DEFAULTS.email);
  const [phone, setPhone] = useState(CONTACT_DEFAULTS.phone);
  const [booking, setBooking] = useState<Booking | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .listProfiles(CURRENT_USER_ID)
      .then(setProfiles)
      .catch(() => setProfiles([]));
  }, []);

  const runSearch = async (event: React.FormEvent) => {
    event.preventDefault();
    setError('');
    setMessage('');
    setBooking(null);
    setSelectedSchedule(null);
    setBusy(true);
    try {
      const found = await api.searchTrains(source.trim().toUpperCase(), destination.trim().toUpperCase(), date);
      setResults(found);
      if (found.length === 0) {
        setMessage('No trains were found for the selected route and date.');
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const createBooking = async () => {
    if (!selectedSchedule || !selectedClass) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      const created = await api.createBooking(
        {
          userId: CURRENT_USER_ID,
          scheduleId: selectedSchedule.scheduleId,
          travelClass: selectedClass,
          contactEmail: email,
          contactPhone: phone,
          passengers
        },
        newIdempotencyKey()
      );
      setBooking(created);
      setMessage(`Booking ${created.bookingReference} created. Seats are held for ${holdCountdownMinutes(created.holdExpiresUtc)} minute(s).`);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const startPayment = async () => {
    if (!booking) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      const intent = await api.createPaymentIntent(booking.bookingReference, 'Card');
      setMessage(
        `Payment intent ${intent.providerIntentId} created for ${formatCurrency(intent.amount)}. The booking is confirmed only after the provider settlement callback succeeds.`
      );
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section>
      <form className="card" onSubmit={runSearch}>
        <h2>Search trains</h2>
        <div className="row">
          <label>
            From (station code)
            <input value={source} onChange={(e) => setSource(e.target.value)} required />
          </label>
          <label>
            To (station code)
            <input value={destination} onChange={(e) => setDestination(e.target.value)} required />
          </label>
          <label>
            Travel date
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
          </label>
          <button type="submit" disabled={busy}>
            Search
          </button>
        </div>
      </form>

      {error ? <p className="error">{error}</p> : null}
      {message ? <p className="success">{message}</p> : null}

      {results.map((train) => (
        <article className="card" key={train.scheduleId}>
          <h3>
            {train.trainNumber} - {train.trainName}
          </h3>
          <p>
            {train.source} {formatDateTime(train.departureUtc)} &rarr; {train.destination} {formatDateTime(train.arrivalUtc)} ({train.status})
          </p>
          <AvailabilityTable
            availability={train.availability}
            selected={selectedSchedule?.scheduleId === train.scheduleId ? selectedClass : null}
            onSelect={(travelClass) => {
              setSelectedSchedule(train);
              setSelectedClass(travelClass);
              setBooking(null);
            }}
          />
        </article>
      ))}

      {selectedSchedule && selectedClass ? (
        <section className="card">
          <h2>Passenger details</h2>
          <PassengerFields
            passengers={passengers}
            profiles={profiles}
            maxPassengers={MAX_PASSENGERS}
            onChange={setPassengers}
          />
          <div className="row">
            <label>
              Contact email
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </label>
            <label>
              Contact mobile
              <input value={phone} onChange={(e) => setPhone(e.target.value)} />
            </label>
            <button type="button" onClick={createBooking} disabled={busy || passengers.some((p) => !p.fullName)}>
              Reserve seats
            </button>
          </div>
        </section>
      ) : null}

      {booking ? (
        <section className="card">
          <h2>Booking {booking.bookingReference}</h2>
          <p>
            Status: <strong>{booking.status}</strong> | Seats: {booking.seatCount} | Total: {formatCurrency(booking.totalFare)}
          </p>
          <ul>
            {booking.passengers.map((p) => (
              <li key={p.id}>
                {p.fullName} ({p.age}) - seat {p.seatNumber}
              </li>
            ))}
          </ul>
          <button type="button" onClick={startPayment} disabled={busy}>
            Pay {formatCurrency(booking.totalFare)}
          </button>
        </section>
      ) : null}
    </section>
  );
}
