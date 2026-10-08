import React, { createContext, useContext, useState } from 'react';

interface ActiveStoreContextValue {
  /** Tienda (almacén) activa. null = "Todas las tiendas" (sin filtrar). */
  storeId: string | null;
  setStoreId: (id: string | null) => void;
}

const SELECTED_STORE_KEY = 'selectedStoreId';

const ActiveStoreContext = createContext<ActiveStoreContextValue | null>(null);

/**
 * Una MiPyme puede administrar varias tiendas (= almacenes). Sus datos no se comparten entre
 * sí: al elegir una tienda, el dashboard, los reportes y las operaciones se filtran para mostrar
 * solo lo de esa tienda. La selección persiste entre sesiones (localStorage) por dispositivo.
 *
 * Nota de nombres: esto NO es el mismo concepto que useStore()/useStores() de hooks/useStores.ts
 * (esos manejan la ficha pública del negocio en el marketplace SaaS). Por eso el hook de abajo se
 * llama useActiveStore, no useStore, para no chocar con ese import.
 */
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [storeId, setStoreIdState] = useState<string | null>(() => localStorage.getItem(SELECTED_STORE_KEY));

  const setStoreId = (id: string | null) => {
    setStoreIdState(id);
    try {
      if (id) localStorage.setItem(SELECTED_STORE_KEY, id);
      else localStorage.removeItem(SELECTED_STORE_KEY);
    } catch { /* localStorage no disponible (modo privado, etc.): la selección solo vive en memoria */ }
  };

  return <ActiveStoreContext.Provider value={{ storeId, setStoreId }}>{children}</ActiveStoreContext.Provider>;
}

export function useActiveStore() {
  const ctx = useContext(ActiveStoreContext);
  if (!ctx) throw new Error('useActiveStore debe usarse dentro de <StoreProvider>');
  return ctx;
}
