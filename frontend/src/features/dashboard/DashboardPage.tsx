import { useQuery } from '@tanstack/react-query';
import { dashboardApi, contractsApi, invoicesApi } from '../../api';
import { Building2, FileText, Users, TrendingUp, AlertCircle, CheckCircle } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, LineChart, Line } from 'recharts';

const mockRevenue = [
  { month: 'Лип', amount: 48000 },
  { month: 'Сер', amount: 52000 },
  { month: 'Вер', amount: 49000 },
  { month: 'Жов', amount: 55000 },
  { month: 'Лис', amount: 58000 },
  { month: 'Гру', amount: 61000 },
];

function StatCard({ title, value, sub, icon: Icon, color }: {
  title: string; value: string | number; sub?: string;
  icon: React.ElementType; color: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 p-5">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm text-gray-500">{title}</p>
          <p className="text-2xl font-semibold text-gray-900 mt-1">{value}</p>
          {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
        </div>
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${color}`}>
          <Icon className="w-5 h-5 text-white" />
        </div>
      </div>
    </div>
  );
}

export default function DashboardPage() {
  const { data: stats } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: dashboardApi.getStats,
    // Placeholder if endpoint not ready yet
    placeholderData: {
      totalProperties: 12,
      totalUnits: 87,
      occupiedUnits: 74,
      occupancyRate: 85.1,
      activeContracts: 74,
      expiringContracts: 5,
      monthlyRevenue: 61000,
      totalDebt: 8400,
      overdueInvoices: 7,
    }
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Дашборд</h1>
        <p className="text-sm text-gray-500 mt-0.5">Загальний огляд системи</p>
      </div>

      {/* Stats grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title="Об'єктів"
          value={stats?.totalProperties ?? 0}
          sub={`${stats?.totalUnits ?? 0} приміщень`}
          icon={Building2}
          color="bg-blue-500"
        />
        <StatCard
          title="Заповненість"
          value={`${stats?.occupancyRate ?? 0}%`}
          sub={`${stats?.occupiedUnits}/${stats?.totalUnits} зайнято`}
          icon={TrendingUp}
          color="bg-green-500"
        />
        <StatCard
          title="Активні договори"
          value={stats?.activeContracts ?? 0}
          sub={`${stats?.expiringContracts} закінчуються скоро`}
          icon={FileText}
          color="bg-purple-500"
        />
        <StatCard
          title="Прострочені рахунки"
          value={stats?.overdueInvoices ?? 0}
          sub={`Борг: ${(stats?.totalDebt ?? 0).toLocaleString('uk-UA')} грн`}
          icon={AlertCircle}
          color="bg-red-500"
        />
      </div>

      {/* Revenue chart */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 bg-white rounded-2xl border border-gray-100 p-5">
          <h2 className="text-sm font-medium text-gray-700 mb-4">Надходження за місяцями (грн)</h2>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={mockRevenue} barSize={28}>
              <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 12, fill: '#9ca3af' }} axisLine={false} tickLine={false} tickFormatter={v => `${(v / 1000).toFixed(0)}к`} />
              <Tooltip formatter={(v: number) => [`${v.toLocaleString('uk-UA')} грн`, 'Сума']} />
              <Bar dataKey="amount" fill="#3b82f6" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Quick actions */}
        <div className="bg-white rounded-2xl border border-gray-100 p-5">
          <h2 className="text-sm font-medium text-gray-700 mb-4">Швидкі дії</h2>
          <div className="space-y-2">
            {[
              { label: 'Новий договір', to: '/contracts/new', color: 'text-blue-600 bg-blue-50' },
              { label: 'Додати об\'єкт', to: '/properties/new', color: 'text-green-600 bg-green-50' },
              { label: 'Новий орендар', to: '/crm/tenants/new', color: 'text-purple-600 bg-purple-50' },
              { label: 'Виставити рахунки', to: '/finance/generate', color: 'text-orange-600 bg-orange-50' },
            ].map(({ label, to, color }) => (
              <a key={to} href={to}
                className={`flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-colors ${color}`}>
                <CheckCircle className="w-4 h-4" />
                {label}
              </a>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
