import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { tenantsApi } from '../../api';
import { Plus, Search, Users, Pencil, Trash2, Building2, Phone, Mail } from 'lucide-react';
import {
  Badge, Modal, Field, inputCls, selectCls,
  EmptyState, Pagination, Spinner, Table, Tr, Td
} from '../../components/ui';
import type { TenantDto } from '../../types';

const schema = z.object({
  firstName: z.string().min(1, "Обов'язкове"),
  lastName:  z.string().min(1, "Обов'язкове"),
  middleName: z.string().optional(),
  email: z.string().email('Невірний email'),
  phone: z.string().min(5, "Обов'язкове"),
  taxCode: z.string().optional(),
  passportNumber: z.string().optional(),
  isCompany: z.boolean(),
  companyName: z.string().optional(),
  companyCode: z.string().optional(),
  notes: z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const defaultValues: FormData = {
  firstName: '', lastName: '', middleName: '', email: '', phone: '',
  taxCode: '', passportNumber: '', isCompany: false,
  companyName: '', companyCode: '', notes: '',
};

export default function TenantsPage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<TenantDto | null>(null);
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['tenants', page, search],
    queryFn: () => tenantsApi.getAll({ page, pageSize: 15, search: search || undefined }),
  });

  const { register, handleSubmit, reset, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues,
  });
  const isCompany = watch('isCompany');

  const createMutation = useMutation({
    mutationFn: (d: FormData) => tenantsApi.create(d as any),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tenants'] }); closeModal(); },
  });

  const updateMutation = useMutation({
    mutationFn: (d: FormData) => tenantsApi.update(editing!.id, d as any),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tenants'] }); closeModal(); },
  });

  const deleteMutation = useMutation({
    mutationFn: tenantsApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tenants'] }),
  });

  const openCreate = () => { setEditing(null); reset(defaultValues); setModalOpen(true); };
  const openEdit = (t: TenantDto) => {
    setEditing(t);
    reset({
      firstName: t.firstName, lastName: t.lastName, middleName: t.middleName ?? '',
      email: t.email, phone: t.phone, taxCode: t.taxCode ?? '',
      passportNumber: t.passportNumber ?? '', isCompany: t.isCompany,
      companyName: t.companyName ?? '', companyCode: t.companyCode ?? '', notes: t.notes ?? '',
    });
    setModalOpen(true);
  };
  const closeModal = () => { setModalOpen(false); setEditing(null); reset(defaultValues); };

  const onSubmit = (d: FormData) =>
    editing ? updateMutation.mutate(d) : createMutation.mutate(d);

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Орендарі</h1>
          <p className="text-sm text-gray-500 mt-0.5">{data?.totalCount ?? 0} записів</p>
        </div>
        <button onClick={openCreate}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
          <Plus className="w-4 h-4" /> Додати орендаря
        </button>
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}
          placeholder="Пошук за ім'ям, email або телефоном..."
          className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-16"><Spinner size="lg" /></div>
      ) : !data?.items.length ? (
        <EmptyState title="Орендарів не знайдено" description="Додайте першого орендаря"
          action={<button onClick={openCreate} className="text-sm text-blue-600 hover:underline">Додати орендаря</button>} />
      ) : (
        <Table headers={['ПІБ / Компанія', 'Контакти', 'ІПН', 'Активні договори', 'Тип', '']}>
          {data.items.map(t => (
            <Tr key={t.id}>
              <Td>
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-blue-50 rounded-full flex items-center justify-center text-xs font-medium text-blue-600 shrink-0">
                    {t.isCompany ? <Building2 className="w-4 h-4" /> : t.firstName.charAt(0)}
                  </div>
                  <div>
                    <p className="font-medium text-gray-900">{t.fullName}</p>
                    {t.isCompany && t.companyCode && <p className="text-xs text-gray-400">Код: {t.companyCode}</p>}
                  </div>
                </div>
              </Td>
              <Td>
                <div className="space-y-0.5">
                  <div className="flex items-center gap-1.5 text-xs text-gray-600">
                    <Phone className="w-3 h-3" />{t.phone}
                  </div>
                  <div className="flex items-center gap-1.5 text-xs text-gray-500">
                    <Mail className="w-3 h-3" />{t.email}
                  </div>
                </div>
              </Td>
              <Td><span className="text-gray-500 text-sm">{t.taxCode || '—'}</span></Td>
              <Td>
                {t.activeContractsCount > 0
                  ? <Badge variant="green">{t.activeContractsCount} активних</Badge>
                  : <span className="text-gray-400 text-sm">немає</span>}
              </Td>
              <Td><Badge variant={t.isCompany ? 'purple' : 'blue'}>{t.isCompany ? 'Юр. особа' : 'Фіз. особа'}</Badge></Td>
              <Td>
                <div className="flex gap-1 justify-end">
                  <button onClick={() => openEdit(t)}
                    className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                    <Pencil className="w-3.5 h-3.5" />
                  </button>
                  <button onClick={() => confirm(`Видалити ${t.fullName}?`) && deleteMutation.mutate(t.id)}
                    className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </Td>
            </Tr>
          ))}
        </Table>
      )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />

      {/* Modal */}
      <Modal open={modalOpen} onClose={closeModal} title={editing ? 'Редагувати орендаря' : 'Новий орендар'} size="lg">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          {/* Company toggle */}
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" {...register('isCompany')} className="w-4 h-4 rounded" />
            <span className="text-sm text-gray-700">Юридична особа</span>
          </label>

          {isCompany && (
            <div className="grid grid-cols-2 gap-3">
              <Field label="Назва компанії" error={errors.companyName?.message} required>
                <input {...register('companyName')} className={inputCls} />
              </Field>
              <Field label="Код ЄДРПОУ" error={errors.companyCode?.message}>
                <input {...register('companyCode')} className={inputCls} />
              </Field>
            </div>
          )}

          <div className="grid grid-cols-3 gap-3">
            <Field label="Прізвище" error={errors.lastName?.message} required>
              <input {...register('lastName')} className={inputCls} />
            </Field>
            <Field label="Ім'я" error={errors.firstName?.message} required>
              <input {...register('firstName')} className={inputCls} />
            </Field>
            <Field label="По батькові" error={errors.middleName?.message}>
              <input {...register('middleName')} className={inputCls} />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Email" error={errors.email?.message} required>
              <input {...register('email')} type="email" className={inputCls} />
            </Field>
            <Field label="Телефон" error={errors.phone?.message} required>
              <input {...register('phone')} className={inputCls} placeholder="+380..." />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="ІПН" error={errors.taxCode?.message}>
              <input {...register('taxCode')} className={inputCls} />
            </Field>
            <Field label="Паспорт / ID" error={errors.passportNumber?.message}>
              <input {...register('passportNumber')} className={inputCls} />
            </Field>
          </div>

          <Field label="Нотатки" error={errors.notes?.message}>
            <textarea {...register('notes')} rows={2} className={inputCls} />
          </Field>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={closeModal}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50 transition-colors">
              Скасувати
            </button>
            <button type="submit" disabled={isSubmitting}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors flex items-center justify-center gap-2">
              {isSubmitting && <Spinner />}
              {editing ? 'Зберегти' : 'Додати'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
