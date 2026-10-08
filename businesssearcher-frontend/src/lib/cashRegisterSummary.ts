import type { OpsCashRegister, OpsCashMovement, OpsSale } from '@/lib/opsTypes';

export interface CurrencyTotal {
  currency: string;
  amount: number;
  count: number;
}

export interface ProductTotal {
  productId: string;
  name: string;
  quantity: number;
  total: number;
}

export interface CashRegisterSummary {
  expected: number;
  /** Estimado informativo: inicial(USD) + efectivo USD cobrado en ventas del turno. El backend no
   * acumula entradas/salidas/gastos en USD (solo a título informativo), así que no es un cuadre
   * estricto como el de CUP — ver comentario en CashRegister.Close(). */
  expectedUsd?: number;
  cashIn: number;
  cashOut: number;
  changeGiven: number;
  changeGivenUsd: number;
  /** Efectivo en USD cobrado en ventas del turno (no forma parte de register.totalSales, que es CUP-only). */
  cashSalesUsd: number;
  transfers: CurrencyTotal[];
  cards: CurrencyTotal[];
  products: ProductTotal[];
}

function byMethodCurrency(sales: OpsSale[], method: string): CurrencyTotal[] {
  const totals = new Map<string, CurrencyTotal>();
  for (const s of sales) {
    if (s.status === 'Refunded') continue;
    for (const p of s.payments) {
      if (p.method !== method) continue;
      const cur = totals.get(p.currency) ?? { currency: p.currency, amount: 0, count: 0 };
      cur.amount += p.amount;
      cur.count += 1;
      totals.set(p.currency, cur);
    }
  }
  return [...totals.values()];
}

/** Resumen de cierre de caja (estilo "cierre Z"): efectivo esperado, entradas/salidas
 * manuales, transferencias y tarjetas por moneda, y productos vendidos del turno. */
export function computeCashRegisterSummary(
  register: OpsCashRegister,
  movements: OpsCashMovement[],
  sales: OpsSale[],
): CashRegisterSummary {
  const expected = register.initialAmount + register.totalSales + register.totalCashIn - register.totalCashOut - register.totalExpenses;
  const cashIn = movements.filter((m) => m.type === 'In').reduce((s, m) => s + m.amount, 0);
  const cashOut = movements.filter((m) => m.type === 'Out').reduce((s, m) => s + m.amount, 0);
  const payments = sales.flatMap((s) => s.payments);
  const changeGiven = payments.reduce((s, p) => s + ((p.changeCurrency ?? 'CUP') !== 'USD' ? (p.change ?? 0) : 0), 0);
  const changeGivenUsd = payments.reduce((s, p) => s + (p.changeCurrency === 'USD' ? (p.change ?? 0) : 0), 0);
  const cashSalesUsd = payments.filter((p) => p.method === 'Cash' && p.currency === 'USD').reduce((s, p) => s + p.amount, 0);
  const expectedUsd = register.initialAmountUSD != null ? register.initialAmountUSD + cashSalesUsd : undefined;

  const productTotals = new Map<string, ProductTotal>();
  for (const s of sales) {
    if (s.status === 'Refunded') continue;
    for (const it of s.items) {
      const row = productTotals.get(it.productId) ?? { productId: it.productId, name: it.productName, quantity: 0, total: 0 };
      row.quantity += it.quantity;
      row.total += it.lineTotal;
      productTotals.set(it.productId, row);
    }
  }

  return {
    expected,
    expectedUsd,
    cashIn,
    cashOut,
    changeGiven,
    changeGivenUsd,
    cashSalesUsd,
    transfers: byMethodCurrency(sales, 'Transfer'),
    cards: byMethodCurrency(sales, 'Card'),
    products: [...productTotals.values()].sort((a, b) => b.total - a.total),
  };
}
