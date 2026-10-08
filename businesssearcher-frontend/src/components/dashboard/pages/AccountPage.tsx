import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Box, Typography, TextField, Button, Alert, Grid, Divider, Avatar,
  Card, CardContent, Chip, List, ListItem, ListItemText,
} from '@mui/material';
import { api } from '@/lib/apiClient';
import { useAuth } from '@/context/AuthContext';
import type { PaymentClaim } from '@/lib/types';

const CLAIM_STATUS_LABEL: Record<PaymentClaim['status'], string> = {
  Pending: '⏳ Pendiente de revisión',
  Approved: '✅ Recibido',
  Rejected: '❌ Rechazado',
};
const CLAIM_STATUS_COLOR: Record<PaymentClaim['status'], 'warning' | 'success' | 'error'> = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

const claimSchema = z.object({
  phoneNumber: z.string().min(5, 'Número inválido'),
  amount: z.coerce.number().positive('Debe ser mayor que cero'),
  currency: z.string().min(1, 'Requerida'),
  proofReference: z.string().min(3, 'Indica una referencia del pago (número de operación, remitente, etc.)'),
});
type ClaimForm = z.infer<typeof claimSchema>;

const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Requerido'),
  newPassword: z.string().min(8, 'Mínimo 8 caracteres'),
  confirmPassword: z.string(),
}).refine((d) => d.newPassword === d.confirmPassword, {
  message: 'Las contraseñas no coinciden',
  path: ['confirmPassword'],
});
type PasswordForm = z.infer<typeof passwordSchema>;

const profileSchema = z.object({
  businessName: z.string().min(2, 'Mínimo 2 caracteres'),
});
type ProfileForm = z.infer<typeof profileSchema>;

export default function AccountPage() {
  const { user, updateUser } = useAuth();
  const [pwSuccess, setPwSuccess] = useState(false);
  const [pwError, setPwError] = useState('');
  const [profileSuccess, setProfileSuccess] = useState(false);
  const [profileError, setProfileError] = useState('');
  const [verifyMsg, setVerifyMsg] = useState('');
  const [logoutAllLoading, setLogoutAllLoading] = useState(false);
  const [claims, setClaims] = useState<PaymentClaim[]>([]);
  const [claimSuccess, setClaimSuccess] = useState(false);
  const [claimError, setClaimError] = useState('');

  const isTenant = user?.role !== 'admin' && !user?.isOpsUser;

  const loadClaims = () => {
    api.get('/api/v1/auth/payment-claims')
      .then((res) => setClaims(res.data?.data ?? res.data ?? []))
      .catch(() => {});
  };

  useEffect(() => {
    if (isTenant) loadClaims();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isTenant]);

  const hasPendingClaim = claims.some((c) => c.status === 'Pending');

  // Suscripción: aviso previo al corte cuando faltan pocos días (además del bloqueo cuando ya venció)
  const daysUntilExpiry = user?.nextPaymentDate
    ? Math.ceil((new Date(user.nextPaymentDate).getTime() - Date.now()) / 86_400_000)
    : null;
  const showExpiringSoon =
    isTenant && user?.isSubscriptionActive !== false &&
    daysUntilExpiry !== null && daysUntilExpiry >= 0 && daysUntilExpiry <= 5;

  // Claim form
  const {
    register: regClaim,
    handleSubmit: handleClaim,
    reset: resetClaim,
    formState: { errors: claimErrors, isSubmitting: claimSubmitting },
  } = useForm<ClaimForm>({
    resolver: zodResolver(claimSchema),
    defaultValues: { phoneNumber: user?.phoneNumber ?? '', currency: 'CUP', amount: undefined, proofReference: '' },
  });

  const onClaimSubmit = async (data: ClaimForm) => {
    setClaimError('');
    try {
      await api.post('/api/v1/auth/payment-claims', data);
      setClaimSuccess(true);
      resetClaim({ phoneNumber: data.phoneNumber, currency: data.currency, amount: undefined, proofReference: '' });
      loadClaims();
      setTimeout(() => setClaimSuccess(false), 4000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setClaimError(msg ?? 'Error al enviar el reporte de pago');
    }
  };

  // Password form
  const {
    register: regPw,
    handleSubmit: handlePw,
    reset: resetPw,
    formState: { errors: pwErrors, isSubmitting: pwSubmitting },
  } = useForm<PasswordForm>({ resolver: zodResolver(passwordSchema) });

  // Profile form
  const {
    register: regProfile,
    handleSubmit: handleProfile,
    formState: { errors: profileErrors, isSubmitting: profileSubmitting },
  } = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema),
    defaultValues: { businessName: user?.businessName ?? '' },
  });

  const onPasswordSubmit = async (data: PasswordForm) => {
    setPwError('');
    try {
      await api.post('/api/v1/auth/change-password', {
        currentPassword: data.currentPassword,
        newPassword: data.newPassword,
      });
      setPwSuccess(true);
      resetPw();
      setTimeout(() => setPwSuccess(false), 3000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setPwError(msg ?? 'Error al cambiar la contraseña');
    }
  };

  const onProfileSubmit = async (data: ProfileForm) => {
    setProfileError('');
    try {
      const res = await api.patch('/api/v1/auth/profile', { businessName: data.businessName });
      const updated = res.data.data ?? res.data;
      if (user) updateUser({ ...user, businessName: updated?.businessName ?? data.businessName });
      setProfileSuccess(true);
      setTimeout(() => setProfileSuccess(false), 3000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setProfileError(msg ?? 'Error al actualizar el perfil');
    }
  };

  const handleResendVerification = async () => {
    try {
      await api.post('/api/v1/auth/resend-verification', { email: user?.email });
      setVerifyMsg('Email de verificación enviado. Revisa tu bandeja de entrada.');
      setTimeout(() => setVerifyMsg(''), 5000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setVerifyMsg(msg ?? 'Error al enviar el email');
      setTimeout(() => setVerifyMsg(''), 5000);
    }
  };

  const handleLogoutAll = async () => {
    setLogoutAllLoading(true);
    try {
      await api.post('/api/v1/auth/logout-all');
      localStorage.removeItem('token');
      window.location.href = '/login';
    } catch {
      setLogoutAllLoading(false);
    }
  };

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={0.5}>Mi cuenta</Typography>
      <Typography variant="body2" color="text.secondary" mb={4}>
        Información de tu cuenta y seguridad
      </Typography>

      {/* Profile card */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" fontWeight={600} mb={2}>Perfil</Typography>
          <Box display="flex" alignItems="flex-start" gap={3} flexWrap="wrap">
            <Avatar sx={{ width: 64, height: 64, bgcolor: 'primary.main', fontSize: 24 }}>
              {user?.businessName?.[0]?.toUpperCase()}
            </Avatar>
            <Box flex={1} minWidth={260}>
              {profileSuccess && <Alert severity="success" sx={{ mb: 2 }}>Perfil actualizado</Alert>}
              {profileError && <Alert severity="error" sx={{ mb: 2 }}>{profileError}</Alert>}
              {verifyMsg && <Alert severity="info" sx={{ mb: 2 }}>{verifyMsg}</Alert>}

              <Box component="form" onSubmit={handleProfile(onProfileSubmit)}>
                <Grid container spacing={2}>
                  <Grid item xs={12} sm={6}>
                    <TextField
                      label="Nombre del negocio"
                      fullWidth
                      {...regProfile('businessName')}
                      error={!!profileErrors.businessName}
                      helperText={profileErrors.businessName?.message}
                    />
                  </Grid>
                  <Grid item xs={12} sm={6}>
                    <TextField
                      label="Email"
                      value={user?.email ?? ''}
                      fullWidth
                      disabled
                    />
                  </Grid>
                  <Grid item xs={12} sm={6}>
                    <TextField
                      label="Plan"
                      value={user?.plan ?? 'Free'}
                      fullWidth
                      disabled
                    />
                  </Grid>
                </Grid>
                <Box display="flex" gap={2} mt={2} flexWrap="wrap">
                  <Button
                    type="submit"
                    variant="contained"
                    disabled={profileSubmitting}
                  >
                    {profileSubmitting ? 'Guardando...' : 'Guardar nombre'}
                  </Button>
                  <Button
                    variant="outlined"
                    onClick={handleResendVerification}
                  >
                    Reenviar verificación de email
                  </Button>
                </Box>
              </Box>
            </Box>
          </Box>
        </CardContent>
      </Card>

      {/* Subscription status — no aplica al admin ni a sesiones de empleado del TPV */}
      {user?.role !== 'admin' && !user?.isOpsUser && (
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="h6" fontWeight={600} mb={2}>Suscripción</Typography>
            <Divider sx={{ mb: 2 }} />
            <Box display="flex" alignItems="center" gap={1.5} flexWrap="wrap" mb={2}>
              <Chip
                label={user?.isSubscriptionActive === false ? 'Vencida' : 'Activa'}
                color={user?.isSubscriptionActive === false ? 'error' : 'success'}
                size="small"
              />
              {user?.nextPaymentDate && (
                <Typography variant="body2" color="text.secondary">
                  {user?.isSubscriptionActive === false ? 'Venció el' : 'Próximo pago'}: {new Date(user.nextPaymentDate).toLocaleDateString('es-ES')}
                </Typography>
              )}
            </Box>
            {user?.isSubscriptionActive === false && (
              <Alert severity="error" sx={{ mb: 2 }}>
                Tu cuenta está bloqueada por falta de pago. Reporta tu pago abajo o escríbenos
                por el chat de soporte (esquina inferior) para coordinar la reactivación.
              </Alert>
            )}
            {showExpiringSoon && (
              <Alert severity="warning" sx={{ mb: 2 }}>
                Tu plan vence en {daysUntilExpiry} día{daysUntilExpiry !== 1 ? 's' : ''}. Reporta tu
                pago abajo para que no se bloquee tu cuenta.
              </Alert>
            )}

            {isTenant && (
              <>
                <Divider sx={{ my: 2 }} />
                <Typography variant="subtitle1" fontWeight={600} mb={1}>Reportar un pago</Typography>
                <Typography variant="body2" color="text.secondary" mb={2}>
                  Indica tu teléfono, el monto y una referencia del comprobante (número de operación,
                  remitente, etc.). Un admin lo revisará y, al aprobarlo, tu suscripción se extiende 30 días.
                </Typography>

                {claimSuccess && <Alert severity="success" sx={{ mb: 2 }}>Reporte de pago enviado. Te avisaremos cuando sea revisado.</Alert>}
                {claimError && <Alert severity="error" sx={{ mb: 2 }}>{claimError}</Alert>}

                {hasPendingClaim ? (
                  <Alert severity="info" sx={{ mb: 2 }}>
                    Ya tienes un reporte de pago pendiente de revisión. Espera a que un admin lo apruebe
                    o lo rechace antes de enviar otro.
                  </Alert>
                ) : (
                  <Box component="form" onSubmit={handleClaim(onClaimSubmit)} sx={{ mb: 2 }}>
                    <Grid container spacing={2}>
                      <Grid item xs={12} sm={4}>
                        <TextField
                          label="Teléfono"
                          fullWidth
                          {...regClaim('phoneNumber')}
                          error={!!claimErrors.phoneNumber}
                          helperText={claimErrors.phoneNumber?.message}
                        />
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <TextField
                          label="Monto" type="number" fullWidth
                          {...regClaim('amount')}
                          error={!!claimErrors.amount}
                          helperText={claimErrors.amount?.message}
                        />
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <TextField
                          label="Moneda" fullWidth
                          {...regClaim('currency')}
                          error={!!claimErrors.currency}
                          helperText={claimErrors.currency?.message}
                        />
                      </Grid>
                      <Grid item xs={12}>
                        <TextField
                          label="Comprobante (referencia del pago)" fullWidth
                          {...regClaim('proofReference')}
                          error={!!claimErrors.proofReference}
                          helperText={claimErrors.proofReference?.message}
                        />
                      </Grid>
                    </Grid>
                    <Button type="submit" variant="contained" disabled={claimSubmitting} sx={{ mt: 2 }}>
                      {claimSubmitting ? 'Enviando...' : 'Enviar reporte de pago'}
                    </Button>
                  </Box>
                )}

                {claims.length > 0 && (
                  <>
                    <Typography variant="subtitle2" fontWeight={600} mt={2} mb={1}>Historial de reportes</Typography>
                    <List dense disablePadding>
                      {claims.map((c) => (
                        <ListItem key={c.id} disableGutters sx={{ py: 0.5 }}>
                          <ListItemText
                            primary={
                              <Box display="flex" alignItems="center" gap={1} flexWrap="wrap">
                                <Typography variant="body2" fontWeight={600}>{c.amount} {c.currency}</Typography>
                                <Chip size="small" label={CLAIM_STATUS_LABEL[c.status]} color={CLAIM_STATUS_COLOR[c.status]} variant="outlined" />
                              </Box>
                            }
                            secondary={`${new Date(c.requestedAt).toLocaleDateString('es-ES')} — ${c.proofReference}`}
                            secondaryTypographyProps={{ variant: 'caption' }}
                          />
                        </ListItem>
                      ))}
                    </List>
                  </>
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}

      {/* Change password */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" fontWeight={600} mb={2}>Cambiar contraseña</Typography>
          <Divider sx={{ mb: 3 }} />

          {pwSuccess && <Alert severity="success" sx={{ mb: 2 }}>Contraseña cambiada exitosamente</Alert>}
          {pwError && <Alert severity="error" sx={{ mb: 2 }}>{pwError}</Alert>}

          <Box component="form" onSubmit={handlePw(onPasswordSubmit)}>
            <Grid container spacing={2}>
              <Grid item xs={12} sm={6}>
                <TextField
                  label="Contraseña actual"
                  type="password"
                  fullWidth
                  {...regPw('currentPassword')}
                  error={!!pwErrors.currentPassword}
                  helperText={pwErrors.currentPassword?.message}
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <TextField
                  label="Nueva contraseña"
                  type="password"
                  fullWidth
                  {...regPw('newPassword')}
                  error={!!pwErrors.newPassword}
                  helperText={pwErrors.newPassword?.message}
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <TextField
                  label="Confirmar nueva contraseña"
                  type="password"
                  fullWidth
                  {...regPw('confirmPassword')}
                  error={!!pwErrors.confirmPassword}
                  helperText={pwErrors.confirmPassword?.message}
                />
              </Grid>
            </Grid>
            <Button
              type="submit"
              variant="contained"
              disabled={pwSubmitting}
              sx={{ mt: 2 }}
            >
              {pwSubmitting ? 'Cambiando...' : 'Cambiar contraseña'}
            </Button>
          </Box>
        </CardContent>
      </Card>

      {/* Security */}
      <Card>
        <CardContent>
          <Typography variant="h6" fontWeight={600} mb={2}>Seguridad de la sesión</Typography>
          <Divider sx={{ mb: 3 }} />
          <Typography variant="body2" color="text.secondary" mb={2}>
            Cerrar sesión en todos los dispositivos revocará todos los tokens de sesión activos.
          </Typography>
          <Button
            variant="outlined"
            color="error"
            disabled={logoutAllLoading}
            onClick={handleLogoutAll}
          >
            {logoutAllLoading ? 'Cerrando sesiones...' : 'Cerrar sesión en todos los dispositivos'}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
