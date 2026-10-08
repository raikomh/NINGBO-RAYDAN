import { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Chip, CircularProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Stack, Alert,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField, InputAdornment, IconButton, Tooltip,
  Collapse,
} from '@mui/material';
import {
  CloudSync, CloudUpload, CloudDownload, VpnKey, ContentCopy, DeleteForever,
  Link as LinkIcon, LinkOff, CheckCircle, ExpandMore, ExpandLess,
} from '@mui/icons-material';
import {
  useSyncHistory, useSyncPush, useSyncPull, useGenerateSyncApiKey, useRevokeSyncApiKey,
  useSyncConnectionStatus, useConnectSync, useDisconnectSync, useConnectSyncWithApiKey,
} from '@/hooks/useOps';
import { useIsOpsAdmin } from '@/hooks/useOpsRole';
import { isLocalDeployment } from '@/lib/deployment';

const STATUS_COLOR: Record<string, 'success' | 'error' | 'warning' | 'default'> = {
  Success: 'success', Failed: 'error', PartialFailure: 'warning', InProgress: 'default',
};
const DIRECTION_LABEL: Record<string, string> = { Push: 'Subida', Pull: 'Descarga' };

const errorMessage = (err: unknown, fallback: string) =>
  (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

function ConnectionCard({ isAdmin }: { isAdmin: boolean }) {
  const { data: status, isLoading } = useSyncConnectionStatus();
  const connect = useConnectSync();
  const disconnect = useDisconnectSync();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmDisconnect, setConfirmDisconnect] = useState(false);

  const runConnect = () => {
    setError(null);
    connect.mutate({ email, password }, {
      onSuccess: () => { setPassword(''); },
      onError: (err) => setError(errorMessage(err, 'No se pudo conectar con el backend online.')),
    });
  };
  const runDisconnect = () => {
    setConfirmDisconnect(false);
    setError(null);
    disconnect.mutate(undefined, { onError: (err) => setError(errorMessage(err, 'No se pudo desconectar.')) });
  };

  return (
    <Card variant="outlined" sx={{ mb: 3 }}>
      <CardContent>
        <Box display="flex" alignItems="center" gap={1} mb={1}>
          <LinkIcon fontSize="small" color="action" />
          <Typography variant="subtitle1" fontWeight={700}>Conexión con tu cuenta online</Typography>
        </Box>

        {isLoading ? (
          <Box display="flex" py={2}><CircularProgress size={20} /></Box>
        ) : !isAdmin ? (
          <Typography variant="body2" color="text.secondary">
            {status?.isConnected
              ? 'Esta instalación está conectada con la cuenta online.'
              : 'Esta instalación todavía no está conectada con una cuenta online. Solo el Administrador puede conectarla.'}
          </Typography>
        ) : status?.isConnected ? (
          <>
            <Alert severity="success" icon={<CheckCircle fontSize="small" />} sx={{ mb: 2 }}>
              Conectado con tu cuenta online. Las sincronizaciones (push/pull) ya pueden usar esta
              conexión sin más pasos.
            </Alert>
            <Button
              variant="outlined" color="error"
              startIcon={disconnect.isPending ? <CircularProgress size={16} /> : <LinkOff />}
              onClick={() => setConfirmDisconnect(true)} disabled={disconnect.isPending}
            >
              Desconectar
            </Button>
          </>
        ) : (
          <>
            {status?.autoRetryPending && (
              <Alert severity="info" sx={{ mb: 2 }}>
                Reintentos automáticos activados: en cuanto tu cuenta online sea aprobada, esta
                instalación se conecta sola y sube todo lo acumulado — no tienes que hacer nada.
                Si prefieres forzarlo ahora, puedes conectar manualmente abajo.
              </Alert>
            )}
            <Typography variant="body2" color="text.secondary" mb={2}>
              Escribe las mismas credenciales con las que entras a tu cuenta en la web (no una API
              key: nunca tienes que copiar ni pegar nada). Esta instalación hace el resto sola.
            </Typography>
            <Stack spacing={1.5} sx={{ maxWidth: 420 }}>
              <TextField
                size="small" label="Email de tu cuenta online" type="email"
                value={email} onChange={(e) => setEmail(e.target.value)}
              />
              <TextField
                size="small" label="Contraseña" type="password"
                value={password} onChange={(e) => setPassword(e.target.value)}
              />
              <Box>
                <Button
                  variant="contained"
                  startIcon={connect.isPending ? <CircularProgress size={16} color="inherit" /> : <LinkIcon />}
                  onClick={runConnect}
                  disabled={connect.isPending || !email || !password}
                >
                  Conectar
                </Button>
              </Box>
            </Stack>
          </>
        )}

        {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      </CardContent>

      <Dialog open={confirmDisconnect} onClose={() => setConfirmDisconnect(false)}>
        <DialogTitle>Desconectar del backend online</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            Esta instalación dejará de poder sincronizar hasta que la conectes de nuevo. ¿Confirmas?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmDisconnect(false)}>Cancelar</Button>
          <Button color="error" variant="contained" onClick={runDisconnect}>Desconectar</Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}

export default function SyncPage() {
  const { data: history, isLoading } = useSyncHistory();
  const push = useSyncPush();
  const pull = useSyncPull();
  const generateKey = useGenerateSyncApiKey();
  const revokeKey = useRevokeSyncApiKey();
  const isAdmin = useIsOpsAdmin();
  const [lastError, setLastError] = useState<string | null>(null);
  const [keyError, setKeyError] = useState<string | null>(null);
  const [revoked, setRevoked] = useState(false);
  const [confirmRevoke, setConfirmRevoke] = useState(false);
  const [confirmGenerate, setConfirmGenerate] = useState(false);
  const [copied, setCopied] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  const runPush = () => {
    setLastError(null);
    push.mutate(undefined, { onError: (err) => setLastError(errorMessage(err, 'No se pudo subir la sincronización.')) });
  };
  const runPull = () => {
    setLastError(null);
    pull.mutate(undefined, { onError: (err) => setLastError(errorMessage(err, 'No se pudo descargar la sincronización.')) });
  };
  const runGenerateKey = () => {
    setKeyError(null);
    setRevoked(false);
    setCopied(false);
    generateKey.mutate(undefined, { onError: (err) => setKeyError(errorMessage(err, 'No se pudo generar la API key.')) });
  };
  const runRevokeKey = () => {
    setConfirmRevoke(false);
    setKeyError(null);
    revokeKey.mutate(undefined, {
      onSuccess: () => { generateKey.reset(); setRevoked(true); },
      onError: (err) => setKeyError(errorMessage(err, 'No se pudo revocar la API key.')),
    });
  };
  const copyKey = () => {
    if (!generateKey.data) return;
    navigator.clipboard.writeText(generateKey.data.apiKey);
    setCopied(true);
  };

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <CloudSync color="primary" />
        <Typography variant="h5" fontWeight={700}>Sincronización</Typography>
      </Box>

      {!isLocalDeployment && (
        <Alert severity="info" sx={{ mb: 3 }}>
          Desde aquí habilitas que <strong>tu instalación local</strong> se descargue los datos de
          esta cuenta. Genera la clave, cópiala y pégala en la pantalla de sincronización de tu
          instalación. Subir y descargar se hace allí, no en esta web.
        </Alert>
      )}

      {isLocalDeployment && <ConnectionCard isAdmin={isAdmin} />}

      {isLocalDeployment && (
      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="body2" color="text.secondary" mb={2}>
            Sube las ventas, compras y movimientos generados en esta instalación local hacia el
            backend online. Cualquier trabajador puede subir cambios (push)
            {isAdmin ? '; como Administrador también puedes descargar el snapshot del backend online (pull).' : '.'}
          </Typography>

          <Stack direction="row" spacing={1.5} flexWrap="wrap">
            <Button
              variant="contained" startIcon={push.isPending ? <CircularProgress size={16} color="inherit" /> : <CloudUpload />}
              onClick={runPush} disabled={push.isPending}
            >
              Sincronizar ahora
            </Button>
            {isAdmin && (
              <Button
                variant="outlined" startIcon={pull.isPending ? <CircularProgress size={16} /> : <CloudDownload />}
                onClick={runPull} disabled={pull.isPending}
              >
                Descargar cambios
              </Button>
            )}
          </Stack>

          {lastError && <Alert severity="error" sx={{ mt: 2 }}>{lastError}</Alert>}

          {push.isSuccess && push.data && (
            <Alert severity="success" sx={{ mt: 2 }}>
              Subida completada: {push.data.itemsAccepted} de {push.data.itemsSent} elementos aceptados.
              {push.data.conflicts.length > 0 && ` Conflictos: ${push.data.conflicts.join(', ')}.`}
            </Alert>
          )}
          {pull.isSuccess && pull.data && (
            <Alert severity="success" sx={{ mt: 2 }}>
              Descarga completada: {pull.data.itemsAccepted} de {pull.data.itemsReceived} elementos aceptados.
              {pull.data.conflicts.length > 0 && ` Conflictos: ${pull.data.conflicts.join(', ')}.`}
            </Alert>
          )}
        </CardContent>
      </Card>
      )}

      {isAdmin && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Box
              display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={1}
              sx={{ cursor: isLocalDeployment ? 'pointer' : 'default' }}
              onClick={isLocalDeployment ? () => setShowAdvanced((v) => !v) : undefined}
            >
              <Box display="flex" alignItems="center" gap={1}>
                <VpnKey fontSize="small" color="action" />
                <Typography variant="subtitle1" fontWeight={700}>
                  {isLocalDeployment ? 'Avanzado: API key manual' : 'Clave de sincronización'}
                </Typography>
              </Box>
              {isLocalDeployment && (
                <IconButton size="small">{showAdvanced ? <ExpandLess /> : <ExpandMore />}</IconButton>
              )}
            </Box>
            {/* En el despliegue online esta tarjeta es el contenido principal de la página, así
                que va siempre desplegada; en local queda plegada como opción avanzada. */}
            <Collapse in={showAdvanced || !isLocalDeployment}>
              <Typography variant="body2" color="text.secondary" mt={2} mb={2}>
                {isLocalDeployment
                  ? 'Normalmente no necesitas esto: usa "Conectar" arriba desde tu instalación Local. Estos botones solo sirven desde tu cuenta online, para revocar el acceso actual o generar una key a mano por algún motivo puntual.'
                  : 'Genera la clave y pégala en tu instalación local para que pueda descargarse los datos de esta cuenta. Solo hay una clave activa por negocio: si generas una nueva, la instalación que estuviera usando la anterior dejará de sincronizar hasta que pegues la nueva.'}
              </Typography>

              <Stack direction="row" spacing={1.5} flexWrap="wrap">
                <Button
                  variant="outlined" color="secondary"
                  startIcon={generateKey.isPending ? <CircularProgress size={16} /> : <VpnKey />}
                  onClick={() => setConfirmGenerate(true)} disabled={generateKey.isPending}
                >
                  Generar nueva API key
                </Button>
                <Button
                  variant="outlined" color="error"
                  startIcon={revokeKey.isPending ? <CircularProgress size={16} /> : <DeleteForever />}
                  onClick={() => setConfirmRevoke(true)} disabled={revokeKey.isPending}
                >
                  Revocar API key
                </Button>
              </Stack>

              {keyError && <Alert severity="error" sx={{ mt: 2 }}>{keyError}</Alert>}
              {revoked && <Alert severity="info" sx={{ mt: 2 }}>API key revocada. Las instalaciones conectadas dejarán de poder sincronizar hasta reconectarse.</Alert>}
            </Collapse>
          </CardContent>
        </Card>
      )}

      <Dialog open={!!generateKey.data} onClose={() => generateKey.reset()} maxWidth="sm" fullWidth>
        <DialogTitle>Nueva API key generada</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            Cópiala ahora: por seguridad no se volverá a mostrar en claro. Si la pierdes tendrás que
            generar una nueva (invalida la anterior).
          </Alert>
          <TextField
            fullWidth size="small" value={generateKey.data?.apiKey ?? ''}
            InputProps={{
              readOnly: true,
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip title={copied ? 'Copiado' : 'Copiar'}>
                    <IconButton size="small" onClick={copyKey}><ContentCopy fontSize="small" /></IconButton>
                  </Tooltip>
                </InputAdornment>
              ),
            }}
          />
          {copied && <Typography variant="caption" color="success.main" display="block" mt={1}>Copiado al portapapeles.</Typography>}
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={() => generateKey.reset()}>Listo</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={confirmGenerate} onClose={() => setConfirmGenerate(false)}>
        <DialogTitle>Generar una clave nueva</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            Cada negocio tiene una sola clave activa. Si generas una nueva, la instalación que
            estuviera usando la anterior dejará de sincronizar hasta que pegues la nueva allí.
            ¿Continuamos?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmGenerate(false)}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => { setConfirmGenerate(false); runGenerateKey(); }}
          >
            Generar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={confirmRevoke} onClose={() => setConfirmRevoke(false)}>
        <DialogTitle>Revocar API key</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            La instalación Local que use la key actual dejará de poder sincronizar de inmediato. ¿Confirmas?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmRevoke(false)}>Cancelar</Button>
          <Button color="error" variant="contained" onClick={runRevokeKey}>Revocar</Button>
        </DialogActions>
      </Dialog>

      {/* El historial vive en la instalación local: el backend online no expone /sync/history. */}
      {isLocalDeployment && (
      <>
      <Typography variant="subtitle2" fontWeight={700} mb={1.5}>Historial</Typography>

      {isLoading ? (
        <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell>
                <TableCell>Dirección</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="right">Enviados</TableCell>
                <TableCell align="right">Aceptados</TableCell>
                <TableCell>Detalle</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(history ?? []).map((h) => (
                <TableRow key={h.id} hover>
                  <TableCell>{new Date(h.startedAt).toLocaleString()}</TableCell>
                  <TableCell>{DIRECTION_LABEL[h.direction] ?? h.direction}</TableCell>
                  <TableCell>
                    <Chip size="small" color={STATUS_COLOR[h.status] ?? 'default'} label={h.status} />
                  </TableCell>
                  <TableCell align="right">{h.itemsSent}</TableCell>
                  <TableCell align="right">{h.itemsAccepted}</TableCell>
                  <TableCell>
                    <Typography variant="caption" color={h.errorMessage ? 'error' : 'text.secondary'}>
                      {h.errorMessage ?? '—'}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
              {(history ?? []).length === 0 && (
                <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>Sin sincronizaciones registradas</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      </>
      )}
    </Box>
  );
}
