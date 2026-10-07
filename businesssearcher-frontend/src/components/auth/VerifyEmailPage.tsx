import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Box, Typography, CircularProgress, Button, Alert } from '@mui/material';
import { CheckCircle, Error as ErrorIcon } from '@mui/icons-material';
import { api } from '@/lib/apiClient';

type Status = 'loading' | 'success' | 'error';

export default function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<Status>('loading');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const token = searchParams.get('token');
    if (!token) {
      setStatus('error');
      setMessage('Token de verificación no encontrado en la URL.');
      return;
    }

    api.post(`/api/v1/auth/verify-email?token=${encodeURIComponent(token)}`)
      .then(() => {
        setStatus('success');
      })
      .catch((err) => {
        const msg = err?.response?.data?.message ?? 'El enlace de verificación es inválido o ha expirado.';
        setStatus('error');
        setMessage(msg);
      });
  }, [searchParams]);

  return (
    <Box
      display="flex"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      minHeight="100vh"
      gap={3}
      px={2}
    >
      {status === 'loading' && (
        <>
          <CircularProgress size={48} />
          <Typography variant="h6" color="text.secondary">Verificando tu email…</Typography>
        </>
      )}

      {status === 'success' && (
        <>
          <CheckCircle sx={{ fontSize: 64, color: 'success.main' }} />
          <Typography variant="h5" fontWeight={700} textAlign="center">
            ¡Email verificado!
          </Typography>
          <Typography variant="body1" color="text.secondary" textAlign="center">
            Tu cuenta ha sido verificada correctamente. Ya puedes iniciar sesión.
          </Typography>
          <Button variant="contained" size="large" onClick={() => navigate('/login')}>
            Ir al inicio de sesión
          </Button>
        </>
      )}

      {status === 'error' && (
        <>
          <ErrorIcon sx={{ fontSize: 64, color: 'error.main' }} />
          <Typography variant="h5" fontWeight={700} textAlign="center">
            Error de verificación
          </Typography>
          <Alert severity="error" sx={{ maxWidth: 400, textAlign: 'center' }}>
            {message}
          </Alert>
          <Button variant="outlined" onClick={() => navigate('/login')}>
            Volver al inicio de sesión
          </Button>
        </>
      )}
    </Box>
  );
}
