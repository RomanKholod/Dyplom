import { api } from './client';

const downloadBlob = (data: ArrayBuffer, filename: string, mime: string) => {
  const blob = new Blob([data], { type: mime });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

export const exportApi = {
  // ─── EXCEL ────────────────────────────────────────────────

  downloadInvoicesExcel: async (month?: string, status?: string) => {
    const params: Record<string, string> = {};
    if (month)  params.month  = month;
    if (status) params.status = status;
    const { data } = await api.get('/export/excel/invoices', { params, responseType: 'arraybuffer' });
    downloadBlob(data, `invoices_${month ?? new Date().toISOString().slice(0,7)}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  },

  downloadContractsExcel: async (status?: string) => {
    const params: Record<string, string> = {};
    if (status) params.status = status;
    const { data } = await api.get('/export/excel/contracts', { params, responseType: 'arraybuffer' });
    downloadBlob(data, `contracts_${new Date().toISOString().slice(0,10)}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  },

  downloadTenantsExcel: async () => {
    const { data } = await api.get('/export/excel/tenants', { responseType: 'arraybuffer' });
    downloadBlob(data, `tenants_${new Date().toISOString().slice(0,10)}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  },

  downloadDebtReportExcel: async () => {
    const { data } = await api.get('/export/excel/debt-report', { responseType: 'arraybuffer' });
    downloadBlob(data, `debt_report_${new Date().toISOString().slice(0,10)}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  },

  // ─── PDF ──────────────────────────────────────────────────

  downloadInvoicePdf: async (invoiceId: string) => {
    const { data } = await api.get(`/export/pdf/invoice/${invoiceId}`, { responseType: 'arraybuffer' });
    downloadBlob(data, `invoice_${invoiceId}.pdf`, 'application/pdf');
  },

  downloadContractPdf: async (contractId: string) => {
    const { data } = await api.get(`/export/pdf/contract/${contractId}`, { responseType: 'arraybuffer' });
    downloadBlob(data, `contract_${contractId}.pdf`, 'application/pdf');
  },

  downloadDebtReportPdf: async () => {
    const { data } = await api.get('/export/pdf/debt-report', { responseType: 'arraybuffer' });
    downloadBlob(data, `debt_report_${new Date().toISOString().slice(0,10)}.pdf`, 'application/pdf');
  },
};
