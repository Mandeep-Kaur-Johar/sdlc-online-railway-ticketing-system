import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import { CURRENT_USER_ID } from '../session';
import type { PassengerProfile } from '../types';

const emptyForm = {
  fullName: '',
  age: 30,
  gender: 'Female',
  email: '',
  phone: '',
  idDocument: ''
};

/** User Story 3004 - manage saved passenger profiles. */
export default function ProfilesPage() {
  const [profiles, setProfiles] = useState<PassengerProfile[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    try {
      setProfiles(await api.listProfiles(CURRENT_USER_ID));
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
    const payload = { userId: CURRENT_USER_ID, ...form };
    try {
      if (editingId) {
        await api.updateProfile(editingId, payload);
        setMessage('Passenger profile updated.');
      } else {
        await api.createProfile(payload);
        setMessage('Passenger profile saved.');
      }
      setForm(emptyForm);
      setEditingId(null);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const edit = (profile: PassengerProfile) => {
    setEditingId(profile.id);
    setForm({
      fullName: profile.fullName,
      age: profile.age,
      gender: profile.gender,
      email: profile.email,
      phone: profile.phone,
      idDocument: ''
    });
  };

  return (
    <section>
      <form className="card" onSubmit={submit}>
        <h2>{editingId ? 'Update passenger profile' : 'Add passenger profile'}</h2>
        <div className="row">
          <label>
            Full name
            <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
          </label>
          <label>
            Age
            <input
              type="number"
              min={0}
              max={120}
              value={form.age}
              onChange={(e) => setForm({ ...form, age: Number(e.target.value) })}
            />
          </label>
          <label>
            Gender
            <select value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })}>
              <option>Female</option>
              <option>Male</option>
              <option>Other</option>
            </select>
          </label>
          <label>
            Email
            <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
          </label>
          <label>
            Mobile
            <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          </label>
          <label>
            Identity document
            <input
              value={form.idDocument}
              onChange={(e) => setForm({ ...form, idDocument: e.target.value })}
              placeholder="Stored masked"
            />
          </label>
          <button type="submit">{editingId ? 'Update' : 'Save'}</button>
          {editingId ? (
            <button
              type="button"
              className="secondary"
              onClick={() => {
                setEditingId(null);
                setForm(emptyForm);
              }}
            >
              Cancel
            </button>
          ) : null}
        </div>
        {error ? <p className="error">{error}</p> : null}
        {message ? <p className="success">{message}</p> : null}
      </form>

      <div className="card">
        <h2>Saved passengers</h2>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Age</th>
              <th>Gender</th>
              <th>Email</th>
              <th>Mobile</th>
              <th>Identity document</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {profiles.map((profile) => (
              <tr key={profile.id}>
                <td>{profile.fullName}</td>
                <td>{profile.age}</td>
                <td>{profile.gender}</td>
                <td>{profile.email}</td>
                <td>{profile.phone}</td>
                <td>{profile.idDocumentMasked}</td>
                <td>
                  <button type="button" className="secondary" onClick={() => edit(profile)}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
