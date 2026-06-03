import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { contractsApi, tenantsApi, propertiesApi, exportApi } from '../../api';
import { Plus, Search, FileText, Play, XCircle, RefreshCw, AlertTriangle, FileDown } from 'lucide-react';
import { Badge, Modal, Field, inputCls, selectCls, EmptyState, Pagination, Spinner, Table, Tr, Td } from '../../components/ui';
import type { ContractDto, ContractStatus } from '../../types';
import { format } from 'date-fns';
import { uk } from 'date-fns/locale';

const STATUS_BADGE: Record<ContractStatus, { label: string; variant: any }> = {
  Draft:      { label: 'Чернетка',   variant: 'gray' },
  Active:     { label: 'Активний',   variant: 'green' },
  Expired:    { label: 'Закінчився', variant: 'red' },
  Terminated: { label: 'Розірвано',  variant: 'red' },
  Suspended:  { label: 'Призупинено',variant: 'yellow' },
};

const createSchema = z.object({
  unitId: z.string().min(1, "Обов'язкове"),
  tenantId: z.string().min(1, "Обов'язкове"),
  startDate: z.string().min(1, "Обов'язкове"),
  endDate: z.string().min(1, "Обов'язкове"),
  monthlyRent: z.coerce.number().positive("Має бути > 0"),
  securityDeposit: z.coerce.number().min(0),
  paymentDayOfMonth: z.coerce.number().min(1).max(28),
  notes: z.string().optional(),
});
type CreateForm = z.infer<typeof createSchema>;

const terminateSchema = z.object({ reason: z.string().min(5, 'Вкажіть причину (мін. 5 символів)') });
type TerminateForm = z.infer<typeof terminateSchema>;

const renewSchema = z.object({
  newEndDate: z.string().min(1, "Обов'язкове"),
  newMonthlyRent: z.coerce.number().positive().optional(),
});
type RenewForm = z.infer<typeof renewSchema>;

export default function ContractsPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [terminateTarget, setTerminateTarget] = useState<ContractDto | null>(null);
  const [renewTarget, setRenewTarget] = useState<ContractDto | null>(null);
  const [selectedProperty, setSelectedProperty] = useState('');
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['contracts', page, search, statusFilter],
    queryFn: () => contractsApi.getAll({ page, pageSize: 15, search: search || undefined, status: statusFilter || undefined }),
  });

  const { data: tenants } = useQuery({ queryKey: ['tenants-list'], queryFn: () => tenantsApi.getAll({ pageSize: 200 }) });
  const { data: properties } = useQuery({ queryKey: ['properties-list'], queryFn: () => propertiesApi.getAll({ pageSize: 200 }) });
  const { data: units } = useQuery({
    queryKey: ['units-for-property', selectedProperty],
    queryFn: () => propertiesApi.getUnits(selectedProperty),
    enabled: !!selectedProperty,
  });

  const createForm = useForm<CreateForm>({
    resolver: zodResolver(createSchema),
    defaultValues: { paymentDayOfMonth: 1, securityDeposit: 0 },
  });
  const terminateForm = useForm<TerminateForm>({ resolver: zodResolver(terminateSchema) });
  const renewForm = useForm<RenewForm>({ resolver: zodResolver(renewSchema) });

  const createMutation = useMutation({
    mutationFn: (d: CreateForm) => contractsApi.create(d as any),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['contracts'] }); setCreateOpen(false); createForm.reset(); },
  });
  const activateMutation = useMutation({
    mutationFn: contractsApi.activate,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['contracts'] }),
  });
  const terminateMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => contractsApi.terminate(id, reason),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['contracts'] }); setTerminateTarget(null); terminateForm.reset(); },
  });
  const renewMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: RenewForm }) =>
      contractsApi.renew(id, new Date(data.newEndDate), data.newMonthlyRent),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['contracts'] }); setRenewTarget(null); renewForm.reset(); },
  });

  const fmt = (d: string) => format(new Date(d), 'dd.MM.yyyy', { locale: uk });

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Договори оренди</h1>
          <p className="text-sm text-gray-500 mt-0.5">{data?.totalCount ?? 0} договорів</p>
        </div>
        <button onClick={() => setCreateOpen(true)}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
          <Plus className="w-4 h-4" /> Новий договір
        </button>
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}
            placeholder="Пошук за номером або орендарем..."
            className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1); }}
          className="px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option value="">Всі статуси</option>
          <option value="Draft">Чернетка</option>
          <option value="Active">Активний</option>
          <option value="Expired">Закінчився</option>
          <option value="Terminated">Розірваний</option>
        </select>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-16"><Spinner size="lg" /></div>
      ) : !data?.items.length ? (
        <EmptyState title="Договорів не знайдено" />
      ) : (
        <Table headers={['Номер', 'Об\'єкт / Приміщення', 'Орендар', 'Термін', 'Орендна плата', 'Статус', 'Дії']}>
          {data.items.map(c => {
            const s = STATUS_BADGE[c.status as ContractStatus] ?? { label: c.status, variant: 'gray' };
            const expiring = c.daysUntilExpiry > 0 && c.daysUntilExpiry <= 30 && c.status === 'Active';
            return (
              <Tr key={c.id}>
                <Td>
                  <div className="flex items-center gap-2">
                    <FileText className="w-4 h-4 text-gray-300" />
                    <span className="font-mono text-sm font-medium text-gray-900">{c.number}</span>
                    {expiring && <AlertTriangle className="w-3.5 h-3.5 text-orange-400" title="Закінчується скоро" />}
                  </div>
                </Td>
                <Td>
                  <p className="text-sm font-medium text-gray-800">{c.propertyName}</p>
                  <p className="text-xs text-gray-400">Прим. {c.unitNumber}</p>
                </Td>
                <Td>
                  <p className="text-sm text-gray-800">{c.tenantName}</p>
                  <p className="text-xs text-gray-400">{c.tenantPhone}</p>
                </Td>
                <Td>
                  <p className="text-sm text-gray-700">{fmt(c.startDate)} – {fmt(c.endDate)}</p>
                  {c.status === 'Active' && (
                    <p className={`text-xs ${expiring ? 'text-orange-500' : 'text-gray-400'}`}>
                      {c.daysUntilExpiry > 0 ? `Залишилось ${c.daysUntilExpiry} дн.` : 'Прострочено'}
                    </p>
                  )}
                </Td>
                <Td>
                  <p className="text-sm font-medium text-gray-900">{c.monthlyRent.toLocaleString('uk-UA')} грн</p>
                  <p className="text-xs text-gray-400">день оплати: {c.paymentDayOfMonth}</p>
                </Td>
                <Td><Badge variant={s.variant}>{s.label}</Badge></Td>
                <Td>
                  <div className="flex gap-1">
                    <button onClick={() => exportApi.downloadContractPdf(c.id)} title="Скачати PDF"
                      className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                      <FileDown className="w-3.5 h-3.5" />
                    </button>
                    {c.status === 'Draft' && (
                      <button onClick={() => activateMutation.mutate(c.id)} title="Активувати"
                        className="p-1.5 text-gray-400 hover:text-green-600 hover:bg-green-50 rounded-lg transition-colors">
                        <Play className="w-3.5 h-3.5" />
                      </button>
                    )}
                    {c.status === 'Active' && (
                      <>
                        <button onClick={() => { setRenewTarget(c); renewForm.setValue('newEndDate', c.endDate.substring(0, 10)); }}
                          title="Продовжити" className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                          <RefreshCw className="w-3.5 h-3.5" />
                        </button>
                        <button onClick={() => setTerminateTarget(c)} title="Розірвати"
                          className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                          <XCircle className="w-3.5 h-3.5" />
                        </button>
                      </>
                    )}
                  </div>
                </Td>
              </Tr>
            );
          })}
        </Table>
      )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />

      {/* Create modal */}
      <Modal open={createOpen} onClose={() => { setCreateOpen(false); createForm.reset(); }}
        title="Новий договір оренди" size="lg">
        <form onSubmit={createForm.handleSubmit(d => createMutation.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Орендар" error={createForm.formState.errors.tenantId?.message} required>
              <select {...createForm.register('tenantId')} className={selectCls}>
                <option value="">Оберіть орендаря...</option>
                {tenants?.items.map(t => <option key={t.id} value={t.id}>{t.fullName}</option>)}
              </select>
            </Field>
            <Field label="Об'єкт" required>
              <select value={selectedProperty} onChange={e => setSelectedProperty(e.target.value)} className={selectCls}>
                <option value="">Оберіть об'єкт...</option>
                {properties?.items.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </Field>
          </div>

          <Field label="Приміщення" error={createForm.formState.errors.unitId?.message} required>
            <select {...createForm.register('unitId')} className={selectCls} disabled={!selectedProperty}>
              <option value="">Спочатку оберіть об'єкт...</option>
              {units?.filter(u => u.status === 'Available').map(u => (
                <option key={u.id} value={u.id}>Прим. {u.number} | {u.area} м² | {u.baseRentPrice.toLocaleString('uk-UA')} грн</option>
              ))}
            </select>
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Дата початку" error={createForm.formState.errors.startDate?.message} required>
              <input type="date" {...createForm.register('startDate')} className={inputCls} />
            </Field>
            <Field label="Дата закінчення" error={createForm.formState.errors.endDate?.message} required>
              <input type="date" {...createForm.register('endDate')} className={inputCls} />
            </Field>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <Field label="Орендна плата (грн)" error={createForm.formState.errors.monthlyRent?.message} required>
              <input type="number" {...createForm.register('monthlyRent')} className={inputCls} />
            </Field>
            <Field label="Заставна сума (грн)" error={createForm.formState.errors.securityDeposit?.message}>
              <input type="number" {...createForm.register('securityDeposit')} className={inputCls} />
            </Field>
            <Field label="День оплати" error={createForm.formState.errors.paymentDayOfMonth?.message} required>
              <input type="number" min="1" max="28" {...createForm.register('paymentDayOfMonth')} className={inputCls} />
            </Field>
          </div>

          <Field label="Нотатки">
            <textarea {...createForm.register('notes')} rows={2} className={inputCls} />
          </Field>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => { setCreateOpen(false); createForm.reset(); }}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50 transition-colors">Скасувати</button>
            <button type="submit" disabled={createMutation.isPending}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors flex items-center justify-center gap-2">
              {createMutation.isPending && <Spinner />} Створити договір
            </button>
          </div>
        </form>
      </Modal>

      {/* Terminate modal */}
      <Modal open={!!terminateTarget} onClose={() => { setTerminateTarget(null); terminateForm.reset(); }}
        title={`Розірвати договір ${terminateTarget?.number}`} size="sm">
        <form onSubmit={terminateForm.handleSubmit(d => terminateMutation.mutate({ id: terminateTarget!.id, reason: d.reason }))}
          className="space-y-4">
          <p className="text-sm text-gray-600">Орендар: <strong>{terminateTarget?.tenantName}</strong></p>
          <Field label="Причина розірвання" error={terminateForm.formState.errors.reason?.message} required>
            <textarea {...terminateForm.register('reason')} rows={3} className={inputCls}
              placeholder="Вкажіть причину розірвання договору..." />
          </Field>
          <div className="flex gap-3">
            <button type="button" onClick={() => setTerminateTarget(null)}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">Скасувати</button>
            <button type="submit" disabled={terminateMutation.isPending}
              className="flex-1 bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {terminateMutation.isPending && <Spinner />} Розірвати
            </button>
          </div>
        </form>
      </Modal>

      {/* Renew modal */}
      <Modal open={!!renewTarget} onClose={() => { setRenewTarget(null); renewForm.reset(); }}
        title={`Продовжити договір ${renewTarget?.number}`} size="sm">
        <form onSubmit={renewForm.handleSubmit(d => renewMutation.mutate({ id: renewTarget!.id, data: d }))}
          className="space-y-4">
          <Field label="Нова дата закінчення" error={renewForm.formState.errors.newEndDate?.message} required>
            <input type="date" {...renewForm.register('newEndDate')} className={inputCls} />
          </Field>
          <Field label="Нова орендна плата (якщо змінюється)">
            <input type="number" {...renewForm.register('newMonthlyRent')} className={inputCls}
              placeholder={`Поточна: ${renewTarget?.monthlyRent.toLocaleString('uk-UA')} грн`} />
          </Field>
          <div className="flex gap-3">
            <button type="button" onClick={() => setRenewTarget(null)}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">Скасувати</button>
            <button type="submit" disabled={renewMutation.isPending}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {renewMutation.isPending && <Spinner />} Продовжити
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
