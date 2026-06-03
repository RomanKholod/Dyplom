import { useState } from 'react';
import TenantsPage from './TenantsPage';
import OwnersPage from './OwnersPage';

const tabs = [
  { id: 'tenants', label: 'Орендарі' },
  { id: 'owners', label: 'Власники' },
];

export default function CrmPage() {
  const [tab, setTab] = useState<'tenants' | 'owners'>('tenants');

  return (
    <div className="space-y-5">
      {/* Tabs */}
      <div className="flex gap-1 p-1 bg-gray-100 rounded-xl w-fit">
        {tabs.map(t => (
          <button key={t.id} onClick={() => setTab(t.id as any)}
            className={`px-5 py-2 text-sm font-medium rounded-lg transition-colors ${
              tab === t.id ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}>
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'tenants' ? <TenantsPage /> : <OwnersPage />}
    </div>
  );
}
