import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi, invoicesApi, contractsApi, exportApi } from '../../api';
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell,
  XAxis, YAxis, Tooltip, ResponsiveContainer, Legend
} from 'recharts';
import { Spinner } from '../../components/ui';
import { FileSpreadsheet, FileText, Download, AlertTriangle } from 'lucide-react';

const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];

function ExportButton({
  label, icon: Icon, color, onClick, loading
}: {
  label: string; icon: React.ElementType; color: string;
  onClick: () => Promise<void>; loading?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const handle = async () => {
    setBusy(true);
    try { await onClick(); } finally { setBusy(false); }
  };
  return (
    <button onClick={handle} disabled={busy}
      className={`flex items-center gap-2 px-3 py-2 text-xs font-medium rounded-xl border transition-colors disabled:opacity-50 ${color}`}>
      {busy ? <Spinner /> : <Icon className="w-3.5 h-3.5" />}
      {label}
    </button>
  );
}

export default function ReportsPage() {
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));

  const { data: stats, isLoading } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: dashboardApi.getStats,
  });
  const { data: invoices } = useQuery({
    queryKey: ['invoices-report'],
    queryFn: () => invoicesApi.getAll({ pageSize: 500 }),
  });
  const { data: contracts } = useQuery({
    queryKey: ['contracts-report'],
    queryFn: () => contractsApi.getAll({ pageSize: 500 }),
  });

  if (isLoading) return <div className="flex justify-center py-16"><Spinner size="lg" /></div>;

  const typeBreakdown = (invoices?.items ?? []).reduce((acc: Record<string, number>, inv) => {
    acc[inv.type] = (acc[inv.type] ?? 0) + inv.amount;
    return acc;
  }, {});
  const typeLabels: Record<string, string> = {
    Rent: 'Оренда', Utility: 'Комунальні', Maintenance: 'Обслуговування',
    Deposit: 'Застава', Fine: 'Штраф',
  };
  const pieData = Object.entries(typeBreakdown).map(([type, amount]) => ({
    name: typeLabels[type] ?? type, value: amount,
  }));

  const contractStatuses = (contracts?.items ?? []).reduce((acc: Record<string, number>, c) => {
    acc[c.status] = (acc[c.status] ?? 0) + 1;
    return acc;
  }, {});
  const statusLabels: Record<string, string> = {
    Active: 'Активні', Draft: 'Чернетки', Expired: 'Закінчились', Terminated: 'Розірвані',
  };
  const contractPieData = Object.entries(contractStatuses).map(([s, count]) => ({
    name: statusLabels[s] ?? s, value: count,
  }));

  const revChart = stats?.revenueChart ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Звіти та аналітика</h1>
          <p className="text-sm text-gray-500 mt-0.5">Фінансові показники та статистика</p>
        </div>
      </div>

      {/* KPI summary */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { label: 'Заповненість',       value: `${stats?.occupancyRate ?? 0}%`,       bg: 'bg-blue-50',   text: 'text-blue-700' },
          { label: 'Активних договорів', value: stats?.activeContracts ?? 0,           bg: 'bg-green-50',  text: 'text-green-700' },
          { label: 'Борг загальний',     value: `${(stats?.totalDebt ?? 0).toLocaleString('uk-UA')} грн`, bg: 'bg-red-50', text: 'text-red-700' },
          { label: 'Дохід цього місяця', value: `${(stats?.monthlyRevenue ?? 0).toLocaleString('uk-UA')} грн`, bg: 'bg-purple-50', text: 'text-purple-700' },
        ].map(({ label, value, bg, text }) => (
          <div key={label} className={`${bg} rounded-2xl p-5`}>
            <p className="text-sm text-gray-500">{label}</p>
            <p className={`text-2xl font-bold mt-1 ${text}`}>{value}</p>
          </div>
        ))}
      </div>

      {/* Export panel */}
      <div className="bg-white rounded-2xl border border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-4 flex items-center gap-2">
          <Download className="w-4 h-4" /> Експорт даних
        </h2>
        <div className="space-y-4">
          {/* Month picker for invoice exports */}
          <div className="flex items-center gap-3">
            <label className="text-xs text-gray-500 w-20">Місяць:</label>
            <input type="month" value={month} onChange={e => setMonth(e.target.value)}
              className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          {/* Excel exports */}
          <div>
            <p className="text-xs font-medium text-gray-500 mb-2 flex items-center gap-1.5">
              <FileSpreadsheet className="w-3.5 h-3.5 text-green-600" /> Excel (.xlsx)
            </p>
            <div className="flex flex-wrap gap-2">
              <ExportButton label="Рахунки за місяць" icon={FileSpreadsheet}
                color="border-green-200 text-green-700 hover:bg-green-50"
                onClick={() => exportApi.downloadInvoicesExcel(month)} />
              <ExportButton label="Всі договори" icon={FileSpreadsheet}
                color="border-green-200 text-green-700 hover:bg-green-50"
                onClick={() => exportApi.downloadContractsExcel()} />
              <ExportButton label="Орендарі" icon={FileSpreadsheet}
                color="border-green-200 text-green-700 hover:bg-green-50"
                onClick={() => exportApi.downloadTenantsExcel()} />
              <ExportButton label="Звіт по боргах" icon={FileSpreadsheet}
                color="border-red-200 text-red-700 hover:bg-red-50"
                onClick={() => exportApi.downloadDebtReportExcel()} />
            </div>
          </div>

          {/* PDF exports */}
          <div>
            <p className="text-xs font-medium text-gray-500 mb-2 flex items-center gap-1.5">
              <FileText className="w-3.5 h-3.5 text-red-600" /> PDF
            </p>
            <div className="flex flex-wrap gap-2">
              <ExportButton label="Звіт по боргах PDF" icon={FileText}
                color="border-red-200 text-red-700 hover:bg-red-50"
                onClick={() => exportApi.downloadDebtReportPdf()} />
            </div>
            <p className="text-xs text-gray-400 mt-2">
              Для PDF окремого рахунку або договору — натисніть іконку у відповідному розділі
            </p>
          </div>
        </div>
      </div>

      {/* Revenue chart */}
      <div className="bg-white rounded-2xl border border-gray-100 p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-4">Нараховано vs Оплачено (6 місяців, грн)</h2>
        <ResponsiveContainer width="100%" height={240}>
          <BarChart data={revChart} barSize={22}>
            <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#9ca3af' }} axisLine={false} tickLine={false} />
            <YAxis tick={{ fontSize: 12, fill: '#9ca3af' }} axisLine={false} tickLine={false}
              tickFormatter={v => `${(v / 1000).toFixed(0)}к`} />
            <Tooltip formatter={(v: number, name: string) => [
              `${v.toLocaleString('uk-UA')} грн`,
              name === 'invoiced' ? 'Нараховано' : 'Оплачено'
            ]} />
            <Legend formatter={v => v === 'invoiced' ? 'Нараховано' : 'Оплачено'} />
            <Bar dataKey="invoiced" fill="#93c5fd" radius={[4, 4, 0, 0]} />
            <Bar dataKey="paid" fill="#3b82f6" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Invoice types pie */}
        <div className="bg-white rounded-2xl border border-gray-100 p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Нарахування за типами</h2>
          {pieData.length ? (
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie data={pieData} cx="50%" cy="50%" innerRadius={55} outerRadius={85}
                  dataKey="value" nameKey="name" paddingAngle={3}>
                  {pieData.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                </Pie>
                <Tooltip formatter={(v: number) => `${v.toLocaleString('uk-UA')} грн`} />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          ) : <p className="text-sm text-gray-400 text-center py-12">Немає даних</p>}
        </div>

        {/* Contract statuses pie */}
        <div className="bg-white rounded-2xl border border-gray-100 p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Статуси договорів</h2>
          {contractPieData.length ? (
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie data={contractPieData} cx="50%" cy="50%" innerRadius={55} outerRadius={85}
                  dataKey="value" nameKey="name" paddingAngle={3}>
                  {contractPieData.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                </Pie>
                <Tooltip />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          ) : <p className="text-sm text-gray-400 text-center py-12">Немає даних</p>}
        </div>
      </div>

      {/* Expiring contracts alert */}
      {(stats?.expiringContracts ?? 0) > 0 && (
        <div className="bg-orange-50 border border-orange-100 rounded-2xl p-5 flex items-start gap-3">
          <AlertTriangle className="w-5 h-5 text-orange-500 shrink-0 mt-0.5" />
          <div>
            <h2 className="text-sm font-semibold text-orange-700">Договори, що закінчуються</h2>
            <p className="text-sm text-orange-600 mt-0.5">
              {stats?.expiringContracts} договорів закінчуються протягом 30 днів. Перейдіть до розділу <strong>Договори</strong> для продовження.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
