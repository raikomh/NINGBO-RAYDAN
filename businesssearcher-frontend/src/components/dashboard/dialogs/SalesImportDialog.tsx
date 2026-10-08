import { useRef, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, Box, Typography, Alert,
  List, ListItem, ListItemText, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { UploadFile } from '@mui/icons-material';
import { useImportSales } from '@/hooks/useOps';
import type { ImportSalesResult } from '@/lib/opsTypes';

interface Props {
  open: boolean;
  onClose: () => void;
}

export default function SalesImportDialog({ open, onClose }: Props) {
  const importMutation = useImportSales();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportSalesResult | null>(null);
  const [serverError, setServerError] = useState('');

  const handleClose = () => {
    setFile(null);
    setResult(null);
    setServerError('');
    onClose();
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (f) { setFile(f); setResult(null); setServerError(''); }
  };

  const runImport = async (acceptNewPrices?: boolean) => {
    if (!file) return;
    setServerError('');
    try {
      const res = await importMutation.mutateAsync({ file, acceptNewPrices });
      setResult(res);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? 'Error al importar las ventas. Intenta de nuevo.');
    }
  };

  const needsConfirmation = result?.needsPriceConfirmation ?? false;
  const isSuccess = result?.success ?? false;

  if (isSuccess) {
    return (
      <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
        <DialogTitle sx={{ fontWeight: 700 }}>Importar ventas (Excel)</DialogTitle>
        <DialogContent dividers>
          <Alert severity="success">Se ha importado de manera exitosa las ventas.</Alert>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button variant="contained" onClick={handleClose}>Cerrar</Button>
        </DialogActions>
      </Dialog>
    );
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle sx={{ fontWeight: 700 }}>Importar ventas (Excel)</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" mb={2}>
          Sube un archivo <strong>.xlsx</strong> con las columnas <strong>Código, Producto, Cantidad, Precio</strong>.
          Cada fila registra una venta real: descuenta stock y entra a tu caja abierta. Identifica el
          producto por código de barras o, si no hay código, por nombre exacto.
        </Typography>

        {serverError && <Alert severity="error" sx={{ mb: 2 }}>{serverError}</Alert>}

        {result && !needsConfirmation && (
          <Alert severity="warning" sx={{ mb: 2 }}>El archivo contiene errores. Revisa el detalle abajo.</Alert>
        )}

        {result && result.errors.length > 0 && (
          <List dense sx={{ mb: 2, bgcolor: 'action.hover', borderRadius: 1 }}>
            {result.errors.map((e, i) => (
              <ListItem key={i}>
                <ListItemText primary={e} primaryTypographyProps={{ variant: 'body2', color: 'error' }} />
              </ListItem>
            ))}
          </List>
        )}

        {needsConfirmation && (
          <>
            <Alert severity="info" sx={{ mb: 2 }}>
              {result!.priceDifferences.length} producto(s) tienen un precio distinto en el Excel que en el
              sistema. ¿Aceptas los precios del Excel (se actualiza el catálogo) o prefieres mantener los
              precios del sistema (la venta se registra igual, solo cambia el precio usado)?
            </Alert>
            <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Producto</TableCell>
                    <TableCell align="right">Cant.</TableCell>
                    <TableCell align="right">Precio sistema</TableCell>
                    <TableCell align="right">Precio Excel</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {result!.priceDifferences.map((d) => (
                    <TableRow key={d.rowNumber}>
                      <TableCell>{d.productName}</TableCell>
                      <TableCell align="right">{d.quantity}</TableCell>
                      <TableCell align="right">{d.systemPrice.toFixed(2)}</TableCell>
                      <TableCell align="right">{d.excelPrice.toFixed(2)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}

        {!needsConfirmation && (
          <Box display="flex" gap={2} flexWrap="wrap" alignItems="flex-start">
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
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose}>Cerrar</Button>
        {needsConfirmation ? (
          <>
            <Button disabled={importMutation.isPending} onClick={() => runImport(false)}>
              Mantener precios del sistema
            </Button>
            <Button variant="contained" disabled={importMutation.isPending} onClick={() => runImport(true)}>
              Aceptar precios del Excel
            </Button>
          </>
        ) : (
          <Button variant="contained" disabled={!file || importMutation.isPending} onClick={() => runImport(undefined)}>
            {importMutation.isPending ? 'Importando...' : 'Importar'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
