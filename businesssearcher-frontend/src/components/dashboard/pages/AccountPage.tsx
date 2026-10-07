import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Box, Typography, TextField, Button, Alert, Grid, Divider, Avatar,
  Card, CardContent, Chip,
} from '@mui/material';
import { api } from '@/lib/apiClient';
import { useAuth } from '@/context/AuthContext';

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
