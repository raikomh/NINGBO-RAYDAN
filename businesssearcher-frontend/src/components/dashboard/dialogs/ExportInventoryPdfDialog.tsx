import { useMemo, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, MenuItem,
  Box, Typography, FormGroup, FormControlLabel, Checkbox, Stack,
} from '@mui/material';
import { PictureAsPdf } from '@mui/icons-material';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import type { OpsProduct, OpsCategory } from '@/lib/opsTypes';

interface ColumnDef {
  key: string;
  label: string;
  get: (p: OpsProduct, ctx: { catName: (id?: string) => string; stockFor: (p: OpsProduct) => number }) => string;
}

const COLUMNS: ColumnDef[] = [
  { key: 'producto', label: 'Producto', get: (p) => p.name },
  { key: 'codigo', label: 'Código', get: (p) => p.barcode ?? '—' },
  { key: 'categoria', label: 'Categoría', get: (p, ctx) => ctx.catName(p.categoryId) },
  { key: 'costo', label: 'Costo', get: (p) => (p.costPriceUSD != null ? `$${p.costPriceUSD.toFixed(2)}` : '—') },
  { key: 'venta', label: 'Precio de venta', get: (p) => (p.sellPriceUSD != null ? `$${p.sellPriceUSD.toFixed(2)}` : '—') },
  { key: 'stock', label: 'Stock', get: (p, ctx) => String(ctx.stockFor(p)) },
  { key: 'minimo', label: 'Stock mínimo', get: (p) => String(p.minStock) },
];

const DEFAULT_COLUMNS = ['producto', 'codigo', 'categoria', 'venta', 'stock'];

interface Props {
  products: OpsProduct[];
  categories: OpsCategory[] | undefined;
  catName: (id?: string) => string;
  stockFor: (p: OpsProduct) => number;
  storeName?: string;
  onClose: () => void;
}

export default function ExportInventoryPdfDialog({ products, categories, catName, stockFor, storeName, onClose }: Props) {
  const [categoryId, setCategoryId] = useState('');
  const [selectedCols, setSelectedCols] = useState<string[]>(DEFAULT_COLUMNS);

  const toggleCol = (key: string) =>
    setSelectedCols((prev) => (prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]));

  const filtered = useMemo(
    () => (categoryId ? products.filter((p) => p.categoryId === categoryId) : products),
    [products, categoryId],
  );

  const columns = COLUMNS.filter((c) => selectedCols.includes(c.key));
  const canGenerate = columns.length > 0;

  const generate = () => {
    const doc = new jsPDF();
    const title = categoryId ? `Inventario — ${catName(categoryId)}` : 'Inventario';
    doc.setFontSize(14);
    doc.text(title, 14, 15);
    if (storeName) {
      doc.setFontSize(10);
      doc.setTextColor(120);
      doc.text(storeName, 14, 21);
      doc.setTextColor(0);
    }
    autoTable(doc, {
      startY: storeName ? 26 : 22,
      head: [columns.map((c) => c.label)],
      body: filtered.map((p) => columns.map((c) => c.get(p, { catName, stockFor }))),
      styles: { fontSize: 9 },
      headStyles: { fillColor: [37, 99, 235] },
    });
    doc.save(`inventario${categoryId ? `-${catName(categoryId)}` : ''}.pdf`);
    onClose();
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Exportar inventario a PDF</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField size="small" select label="Categoría" value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
            <MenuItem value="">Todas las categorías</MenuItem>
            {categories?.map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
          </TextField>

          <Box>
            <Typography variant="body2" fontWeight={600} mb={0.5}>Columnas a incluir</Typography>
            <FormGroup>
              {COLUMNS.map((c) => (
                <FormControlLabel
                  key={c.key}
                  control={<Checkbox size="small" checked={selectedCols.includes(c.key)} onChange={() => toggleCol(c.key)} />}
                  label={c.label}
                />
              ))}
            </FormGroup>
          </Box>

          <Typography variant="caption" color="text.secondary">
            {filtered.length} producto{filtered.length !== 1 ? 's' : ''} con los filtros actuales.
          </Typography>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" startIcon={<PictureAsPdf />} disabled={!canGenerate} onClick={generate}>
          Generar PDF
        </Button>
      </DialogActions>
    </Dialog>
  );
}
