import { useRef, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField,
  Box, Typography, Alert, Table, TableHead, TableBody, TableRow, TableCell,
  ToggleButtonGroup, ToggleButton,
} from '@mui/material';
import { UploadFile } from '@mui/icons-material';
import { useImportOpsProducts, useWarehouses } from '@/hooks/useOps';
import { useActiveStore } from '@/context/StoreContext';
import type { ImportPriceConflict, ImportPriceDecision, ImportProductsResult } from '@/lib/opsTypes';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Se llama con el resultado cuando la importación termina; el diálogo se cierra después. */
  onImported?: (result: ImportProductsResult) => void;
}

export default function ImportProductsDialog({ open, onClose, onImported }: Props) {
  const importMutation = useImportOpsProducts();
  const { data: warehouses } = useWarehouses();
  // Se importa siempre a la tienda activa: no se puede elegir otra tienda desde aquí (cada
  // tienda es independiente; un punto de venta solo administra su propio catálogo/stock).
  const { storeId } = useActiveStore();
  const warehouseId = storeId ?? '';
  const activeWarehouseName = warehouses?.find((w) => w.id === storeId)?.name;
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [catalogDate, setCatalogDate] = useState('');
  const [exchangeRate, setExchangeRate] = useState('');
  const [serverError, setServerError] = useState('');
  // Conflictos de precio pendientes de decisión (ventana de confirmación).
  const [conflicts, setConflicts] = useState<ImportPriceConflict[] | null>(null);
  const [decisions, setDecisions] = useState<Record<string, ImportPriceDecision>>({});

  const rateNumber = exchangeRate.trim() === '' ? undefined : Number(exchangeRate);
  const rateValid = rateNumber === undefined || (Number.isFinite(rateNumber) && rateNumber > 0);
  const fileIsXlsx = !!file && file.name.toLowerCase().endsWith('.xlsx');
  const canImport = !!file && fileIsXlsx && !!warehouseId && !!catalogDate && rateValid && !importMutation.isPending;

  const handleClose = () => {
    setFile(null);
    setCatalogDate('');
    setExchangeRate('');
    setServerError('');
    setConflicts(null);
    setDecisions({});
    onClose();
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (f) setFile(f);
    e.target.value = ''; // permite volver a elegir el mismo archivo
  };

  const errorMessage = (err: unknown) =>
    (err as { response?: { data?: { message?: string } } })?.response?.data?.message;

  const send = async (priceDecisions?: Record<string, ImportPriceDecision>) => {
    if (!file) return;
    setServerError('');
    try {
      const res = await importMutation.mutateAsync({
        file, warehouseId, catalogDate, exchangeRate: rateNumber, priceDecisions,
      });
      if (res.needsDecision) {
        // Hay precios distintos: se pide decisión por producto antes de escribir nada.
        setConflicts(res.priceConflicts);
        setDecisions(Object.fromEntries(res.priceConflicts.map((c) => [c.barcode, 'keep' as const])));
        return;
      }
      setConflicts(null);
      onImported?.(res);
      handleClose(); // importación correcta: se cierra la ventana
    } catch (err: unknown) {
      // Con error (p. ej. catálogo ya subido a este almacén) el diálogo se queda abierto.
      setConflicts(null);
      setServerError(errorMessage(err) ?? 'Error al importar productos. Intenta de nuevo.');
    }
  };

  const setAll = (value: ImportPriceDecision) =>
    setDecisions(Object.fromEntries((conflicts ?? []).map((c) => [c.barcode, value])));

  return (
    <>
      <Dialog open={open && !conflicts} onClose={handleClose} fullWidth maxWidth="sm">
        <DialogTitle sx={{ fontWeight: 700 }}>Importar productos (Excel)</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" mb={2}>
            Sube un archivo <strong>.xlsx</strong> con las columnas <strong>Código, Imagen, Producto, Categoría,
            Cant. disponible, Precio x unidad (USD)</strong>. La columna <strong>Descripción</strong> es opcional.
            Cada catálogo tiene una fecha: el mismo catálogo no puede subirse dos veces al mismo almacén.
            Si el producto ya existe, su stock se <strong>suma</strong>; no se duplica.
          </Typography>

          {!fileIsXlsx && file && (
            <Alert severity="warning" sx={{ mb: 2 }}>El archivo debe ser .xlsx.</Alert>
          )}

          {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}

          <Box display="flex" flexDirection="column" gap={2}>
            <TextField
              label="Almacén / punto de venta"
              value={activeWarehouseName ?? ''}
              disabled
              helperText={
                warehouses && warehouses.length === 0
                  ? 'Crea un almacén antes de importar.'
                  : 'Siempre se importa a tu tienda activa; aquí se suma el stock.'
              }
            />

            <TextField
              type="date"
              required
              label="Fecha del catálogo"
              value={catalogDate}
              onChange={(e) => setCatalogDate(e.target.value)}
              disabled={importMutation.isPending}
              InputLabelProps={{ shrink: true }}
              helperText="Identifica el catálogo. Subirlo dos veces al mismo almacén no está permitido."
            />

            <TextField
              type="number"
              label="Tasa USD → CUP (opcional)"
              value={exchangeRate}
              onChange={(e) => setExchangeRate(e.target.value)}
              disabled={importMutation.isPending}
              error={!rateValid}
              helperText={!rateValid
                ? 'Debe ser mayor que 0.'
                : 'Calcula el precio en CUP de los productos nuevos. Si se deja vacía se usa la última tasa registrada.'}
              inputProps={{ min: 0, step: 'any' }}
            />

            <Box>
              <input ref={fileInputRef} type="file" accept=".xlsx" hidden onChange={handleFileChange} />
              <Button
                variant="outlined"
                startIcon={<UploadFile />}
                onClick={() => fileInputRef.current?.click()}
                disabled={importMutation.isPending}
              >
                {file ? file.name : 'Seleccionar archivo .xlsx'}
              </Button>
            </Box>
          </Box>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={handleClose}>Cerrar</Button>
          <Button variant="contained" disabled={!canImport} onClick={() => send()}>
            {importMutation.isPending ? 'Importando...' : 'Importar'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Ventana de conflictos de precio: el precio del archivo es distinto al del sistema. */}
      <Dialog open={!!conflicts} onClose={() => setConflicts(null)} fullWidth maxWidth="md">
        <DialogTitle sx={{ fontWeight: 700 }}>Precios distintos en el archivo</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" mb={2}>
            Estos productos ya existen con otro precio. Elige para cada uno si mantienes el precio del sistema
            o aceptas el del archivo. El precio aceptado <strong>solo se aplica a este almacén</strong>; el precio
            general del producto no cambia.
          </Typography>
          <Box display="flex" gap={1} mb={2}>
            <Button size="small" variant="outlined" onClick={() => setAll('keep')}>Mantener todos los del sistema</Button>
            <Button size="small" variant="outlined" onClick={() => setAll('accept')}>Aceptar todos los del archivo</Button>
          </Box>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Código</TableCell>
                <TableCell>Producto</TableCell>
                <TableCell align="right">Sistema (USD)</TableCell>
                <TableCell align="right">Archivo (USD)</TableCell>
                <TableCell align="center">Decisión</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(conflicts ?? []).map((c) => (
                <TableRow key={c.barcode}>
                  <TableCell>{c.barcode}</TableCell>
                  <TableCell>{c.name}</TableCell>
                  <TableCell align="right">{c.systemPriceUsd.toFixed(2)}</TableCell>
                  <TableCell align="right">{c.excelPriceUsd.toFixed(2)}</TableCell>
                  <TableCell align="center">
                    <ToggleButtonGroup
                      size="small"
                      exclusive
                      value={decisions[c.barcode] ?? 'keep'}
                      onChange={(_, v: ImportPriceDecision | null) => {
                        if (v) setDecisions((prev) => ({ ...prev, [c.barcode]: v }));
                      }}
                    >
                      <ToggleButton value="keep">Mantener</ToggleButton>
                      <ToggleButton value="accept">Aceptar</ToggleButton>
                    </ToggleButtonGroup>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setConflicts(null)}>Volver</Button>
          <Button variant="contained" disabled={importMutation.isPending} onClick={() => send(decisions)}>
            {importMutation.isPending ? 'Importando...' : 'Confirmar importación'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
