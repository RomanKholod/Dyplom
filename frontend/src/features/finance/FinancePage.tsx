import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { invoicesApi, exportApi } from '../../api';
import { CreditCard, RefreshCw, Search, TrendingDown, TrendingUp, Clock, CheckCircle2, FileText } from 'lucide-react';
import { Badge, Modal, Field, inputCls, selectCls, Pagination, Spinner, Table, Tr, Td, EmptyState } from '../../components/ui';
import type { InvoiceDto, PaymentStatus } from '../../types';
import { format } from 'date-fns';
import { uk } from 'date-fns/locale';

const STATUS_BADGE: Record<PaymentStatus, { label: string; variant: any }> = {
  Pending:       { label: 'Очікує',        variant: 'yellow' },
  Paid:          { label: 'Оплачено',      variant: 'green' },
  Overdue:       { label: 'Прострочено',   variant: 'red' },
  PartiallyPaid: { label: 'Частково',      variant: 'orange' },
  Cancelled:     { label: 'Скасовано',     variant: 'gray' },
};

const TYPE_LABELS: Record<string, string> = {
  Rent: 'Оренда', Utility: 'Комунальні', Maintenance: 'Обслуговування',
  Deposit: 'Застава', Fine: 'Штраф',
};

const paymentSchema = z.object({
  amount: z.coerce.number().positive('Сума має бути > 0'),
  paymentDate: z.string().min(1, "Обов'язкове"),
  paymentMethod: z.string().optional(),
  reference: z.string().optional(),
  notes: z.string().optional(),
});
type PaymentForm = z.infer<typeof paymentSchema>;

export default function FinancePage() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  
  // Поточний місяць у форматі YYYY-MM
  const nowMonth = format(new Date(), 'yyyy-MM');
  const [monthFilter, setMonthFilter] = useState(nowMonth);
  const [search, setSearch] = useState('');
  const [payInvoice, setPayInvoice] = useState<InvoiceDto | null>(null);
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['invoices', page, statusFilter, monthFilter],
    queryFn: () => invoicesApi.getAll({
      page, pageSize: 15,
      status: statusFilter || undefined,
      month: monthFilter || undefined,
    }),
  });

  const form = useForm<PaymentForm>({
    resolver: zodResolver(paymentSchema),
    defaultValues: { paymentDate: new Date().toISOString().split('T')[0] },
  });

  const payMutation = useMutation({
    mutationFn: (d: PaymentForm) => invoicesApi.addPayment(payInvoice!.id, {
      ...d, paymentDate: new Date(d.paymentDate).toISOString(),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['invoices'] }); setPayInvoice(null); form.reset(); },
  });

  // ─── ВИПРАВЛЕНА МУТАЦІЯ (Передаємо Month з великої літери) ───
  const generateMutation = useMutation({
    mutationFn: (targetMonth: string) => {
      return invoicesApi.generateRentInvoices({ month: targetMonth });
    },
    onSuccess: (res: any) => { 
      qc.invalidateQueries({ queryKey: ['invoices'] }); 
      
      // Гнучка перевірка успішності (підтримує і camelCase, і PascalCase)
      const isSuccess = res?.isSuccess ?? res?.IsSuccess ?? false;
      const value = res?.value ?? res?.Value ?? 0;
      const backendError = res?.error ?? res?.Error ?? res?.message ?? res?.Message;

      if (isSuccess) {
        alert(`Успішно згенеровано рахунків: ${value}`);
      } else {
        // Тепер тут виведеться реальний текст помилки з Result.Failure("...")
        alert(`Помилка генерації: ${backendError || 'Рахунки для цього місяця вже згенеровані або активні договори відсутні'}`);
      }
    },
    onError: (error: any) => {
      alert(`Помилка запиту: ${error?.response?.data?.title || error.message || 'Щось пішло не так'}`);
    }
  });

  const fmt = (d: string) => format(new Date(d), 'dd.MM.yyyy', { locale: uk });

  const getButtonLabel = () => {
    if (!monthFilter) return format(new Date(), 'LLLL', { locale: uk });
    try {
      return format(new Date(`${monthFilter}-01`), 'LLLL', { locale: uk });
    } catch {
      return format(new Date(), 'LLLL', { locale: uk });
    }
  };

  // Summary stats
  const items = data?.items ?? [];
  const totalAmount  = items.reduce((s, i) => s + i.amount, 0);
  const totalPaid    = items.reduce((s, i) => s + i.paidAmount, 0);
  const totalDebt    = items.reduce((s, i) => s + i.debtAmount, 0);
  const overdueCount = items.filter(i => i.status === 'Overdue').length;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Фінанси та платежі</h1>
          <p className="text-sm text-gray-500 mt-0.5">{data?.totalCount ?? 0} рахунків</p>
        </div>
        <button 
          onClick={() => generateMutation.mutate(monthFilter || nowMonth)} 
          disabled={generateMutation.isPending}
          className="flex items-center gap-2 bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors"
        >
          {generateMutation.isPending ? <Spinner /> : <RefreshCw className="w-4 h-4" />}
          Нарахувати за {getButtonLabel()}
        </button>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-4 gap-3">
        {[
          { label: 'Нараховано', value: totalAmount, icon: TrendingUp, color: 'text-blue-600 bg-blue-50' },
          { label: 'Оплачено',   value: totalPaid,   icon: CheckCircle2, color: 'text-green-600 bg-green-50' },
          { label: 'Борг',       value: totalDebt,   icon: TrendingDown, color: 'text-red-600 bg-red-50' },
          { label: 'Прострочених', value: overdueCount, icon: Clock, color: 'text-orange-600 bg-orange-50', isCount: true },
        ].map(({ label, value, icon: Icon, color, isCount }) => (
          <div key={label} className="bg-white rounded-2xl border border-gray-100 p-4 flex items-center gap-3">
            <div className={`w-9 h-9 rounded-xl flex items-center justify-center ${color}`}>
              <Icon className="w-4 h-4" />
            </div>
            <div>
              <p className="text-xs text-gray-500">{label}</p>
              <p className="font-semibold text-gray-900">
                {isCount ? value : `${value.toLocaleString('uk-UA')} грн`}
              </p>
            </div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input value={search} onChange={e => setSearch(e.target.value)}
            placeholder="Пошук за номером або орендарем..."
            className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1); }} className={`${selectCls} w-44`}>
          <option value="">Всі статуси</option>
          <option value="Pending">Очікує</option>
          <option value="Overdue">Прострочено</option>
          <option value="PartiallyPaid">Частково</option>
          <option value="Paid">Оплачено</option>
        </select>
        <input type="month" value={monthFilter} onChange={e => { setMonthFilter(e.target.value); setPage(1); }}
          className={`${inputCls} w-44`} max={nowMonth} />
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-16"><Spinner size="lg" /></div>
      ) : !items.length ? (
        <EmptyState title="Рахунків не знайдено" description="Спробуйте змінити фільтри або нарахувати за поточний місяць" />
      ) : (
        <Table headers={['Рахунок', "Об'єкт / Орендар", 'Тип', 'Сума', 'Оплачено', 'Борг', 'Строк', 'Статус', '']}>
          {items
            .filter(i => !search || i.tenantName.toLowerCase().includes(search.toLowerCase()) || i.number.includes(search))
            .map(i => {
              const s = STATUS_BADGE[i.status as PaymentStatus] ?? { label: i.status, variant: 'gray' };
              return (
                <Tr key={i.id}>
                  <Td><span className="font-mono text-xs text-gray-700">{i.number}</span></Td>
                  <Td>
                    <p className="text-sm font-medium text-gray-800">{i.propertyName}</p>
                    <p className="text-xs text-gray-400">{i.tenantName}</p>
                  </Td>
                  <Td><span className="text-sm text-gray-600">{TYPE_LABELS[i.type] ?? i.type}</span></Td>
                  <Td><span className="text-sm font-medium text-gray-900">{i.amount.toLocaleString('uk-UA')}</span></Td>
                  <Td><span className="text-sm text-green-600">{i.paidAmount.toLocaleString('uk-UA')}</span></Td>
                  <Td>
                    {i.debtAmount > 0
                      ? <span className="text-sm font-medium text-red-600">{i.debtAmount.toLocaleString('uk-UA')}</span>
                      : <span className="text-gray-400">—</span>}
                  </Td>
                  <Td>
                    <span className={`text-xs ${new Date(i.dueDate) < new Date() && i.status !== 'Paid' ? 'text-red-500 font-medium' : 'text-gray-500'}`}>
                      {fmt(i.dueDate)}
                    </span>
                  </Td>
                  <Td><Badge variant={s.variant}>{s.label}</Badge></Td>
                  <Td>
                    <div className="flex gap-1.5">
                      {(i.status === 'Pending' || i.status === 'Overdue' || i.status === 'PartiallyPaid') && (
                        <button
                          onClick={() => { setPayInvoice(i); form.setValue('amount', i.debtAmount); }}
                          className="flex items-center gap-1.5 text-xs font-medium text-blue-600 hover:text-blue-700 bg-blue-50 hover:bg-blue-100 px-2.5 py-1.5 rounded-lg transition-colors">
                          <CreditCard className="w-3.5 h-3.5" /> Оплатити
                        </button>
                      )}
                      <button onClick={() => exportApi.downloadInvoicePdf(i.id)} title="PDF"
                        className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                        <FileText className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </Td>
                </Tr>
              );
            })}
        </Table>
      )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />

      {/* Payment modal */}
      <Modal open={!!payInvoice} onClose={() => { setPayInvoice(null); form.reset(); }}
        title={`Оплата рахунку ${payInvoice?.number}`} size="sm">
        <form onSubmit={form.handleSubmit(d => payMutation.mutate(d))} className="space-y-4">
          <div className="bg-gray-50 rounded-xl p-4 space-y-1.5 text-sm">
            <div className="flex justify-between"><span className="text-gray-500">Орендар:</span><span className="font-medium">{payInvoice?.tenantName}</span></div>
            <div className="flex justify-between"><span className="text-gray-500">Нараховано:</span><span>{payInvoice?.amount.toLocaleString('uk-UA')} грн</span></div>
            <div className="flex justify-between"><span className="text-gray-500">Оплачено:</span><span className="text-green-600">{payInvoice?.paidAmount.toLocaleString('uk-UA')} грн</span></div>
            <div className="flex justify-between border-t border-gray-200 pt-1.5 font-medium"><span>Залишок:</span><span className="text-red-600">{payInvoice?.debtAmount.toLocaleString('uk-UA')} грн</span></div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Сума оплати (грн)" error={form.formState.errors.amount?.message} required>
              <input type="number" step="0.01" {...form.register('amount')} className={inputCls} />
            </Field>
            <Field label="Дата оплати" error={form.formState.errors.paymentDate?.message} required>
              <input type="date" {...form.register('paymentDate')} className={inputCls} />
            </Field>
          </div>

          <Field label="Спосіб оплати">
            <select {...form.register('paymentMethod')} className={selectCls}>
              <option value="">Не вказано</option>
              <option value="cash">Готівка</option>
              <option value="bank_transfer">Банківський переказ</option>
              <option value="card">Картка</option>
            </select>
          </Field>

          <Field label="Реквізити / Номер транзакції">
            <input {...form.register('reference')} className={inputCls} placeholder="Номер платіжного доручення..." />
          </Field>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => setPayInvoice(null)}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">Скасувати</button>
            <button type="submit" disabled={payMutation.isPending}
              className="flex-1 bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {payMutation.isPending && <Spinner />} Підтвердити оплату
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}