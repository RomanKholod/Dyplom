import { clsx } from 'clsx';
import { X, Loader2 } from 'lucide-react';

// ─── BADGE ────────────────────────────────────────────────────

type BadgeVariant = 'green' | 'blue' | 'red' | 'yellow' | 'purple' | 'gray' | 'orange';

const badgeStyles: Record<BadgeVariant, string> = {
  green:  'bg-green-100 text-green-700',
  blue:   'bg-blue-100 text-blue-700',
  red:    'bg-red-100 text-red-700',
  yellow: 'bg-yellow-100 text-yellow-700',
  purple: 'bg-purple-100 text-purple-700',
  gray:   'bg-gray-100 text-gray-600',
  orange: 'bg-orange-100 text-orange-700',
};

export function Badge({ children, variant = 'gray' }: { children: React.ReactNode; variant?: BadgeVariant }) {
  return (
    <span className={clsx('inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-full', badgeStyles[variant])}>
      {children}
    </span>
  );
}

// ─── SPINNER ─────────────────────────────────────────────────

export function Spinner({ size = 'sm' }: { size?: 'sm' | 'md' | 'lg' }) {
  const sizes = { sm: 'w-4 h-4', md: 'w-6 h-6', lg: 'w-8 h-8' };
  return <Loader2 className={clsx('animate-spin text-blue-500', sizes[size])} />;
}

// ─── MODAL ───────────────────────────────────────────────────

export function Modal({
  open, onClose, title, children, size = 'md'
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  size?: 'sm' | 'md' | 'lg';
}) {
  if (!open) return null;
  const widths = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl' };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className={clsx('relative bg-white rounded-2xl shadow-xl w-full', widths[size])}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="font-semibold text-gray-900">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6">{children}</div>
      </div>
    </div>
  );
}

// ─── FORM FIELD ───────────────────────────────────────────────

export function Field({
  label, error, required, children
}: {
  label: string; error?: string; required?: boolean; children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1.5">
        {label}{required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-500">{error}</p>}
    </div>
  );
}

export const inputCls = 'w-full px-3.5 py-2.5 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:bg-gray-50';
export const selectCls = inputCls;

// ─── EMPTY STATE ──────────────────────────────────────────────

export function EmptyState({ title, description, action }: {
  title: string; description?: string; action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <p className="text-lg font-medium text-gray-300">{title}</p>
      {description && <p className="text-sm text-gray-400 mt-1">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

// ─── PAGINATION ───────────────────────────────────────────────

export function Pagination({ page, totalPages, onChange }: {
  page: number; totalPages: number; onChange: (p: number) => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex justify-center gap-1.5 mt-6">
      {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
        <button key={p} onClick={() => onChange(p)}
          className={clsx('w-8 h-8 text-sm rounded-lg transition-colors',
            p === page
              ? 'bg-blue-600 text-white'
              : 'bg-white text-gray-600 border border-gray-200 hover:bg-gray-50')}>
          {p}
        </button>
      ))}
    </div>
  );
}

// ─── TABLE ────────────────────────────────────────────────────

export function Table({ headers, children }: { headers: string[]; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100 bg-gray-50/50">
            {headers.map(h => (
              <th key={h} className="text-left text-xs font-medium text-gray-500 uppercase tracking-wide px-4 py-3 first:pl-6 last:pr-6">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-50">{children}</tbody>
      </table>
    </div>
  );
}

export function Tr({ children, onClick }: { children: React.ReactNode; onClick?: () => void }) {
  return (
    <tr onClick={onClick}
      className={clsx('transition-colors', onClick && 'cursor-pointer hover:bg-gray-50/80')}>
      {children}
    </tr>
  );
}

export function Td({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <td className={clsx('px-4 py-3.5 text-gray-700 first:pl-6 last:pr-6', className)}>
      {children}
    </td>
  );
}
