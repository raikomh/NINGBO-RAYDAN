import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Box, Button, Card, CardContent, TextField, Typography, Alert } from '@mui/material';
import { api } from '@/lib/apiClient';

const schema = z.object({ email: z.string().email('Email inválido') });
type FormData = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    setError('');
    try {
      await api.post('/api/v1/auth/forgot-password', { email: data.email });
      setSent(true);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? 'Error al enviar el email.');
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
        p: 2,
      }}
    >
      <Card sx={{ width: '100%', maxWidth: 420 }}>
        <CardContent sx={{ p: 4 }}>
          <Typography variant="h5" fontWeight={700} mb={0.5}>
            Recuperar contraseña
          </Typography>
          <Typography variant="body2" color="text.secondary" mb={3}>
            Te enviaremos un enlace para restablecer tu contraseña
          </Typography>

          {sent ? (
            <Alert severity="success">
              Si el email existe, recibirás un enlace de recuperación en breve.
            </Alert>
          ) : (
            <>
              {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
              <Box component="form" onSubmit={handleSubmit(onSubmit)} display="flex" flexDirection="column" gap={2}>
                <TextField
                  label="Email"
                  type="email"
                  fullWidth
                  {...register('email')}
                  error={!!errors.email}
                  helperText={errors.email?.message}
                />
                <Button type="submit" variant="contained" fullWidth size="large" disabled={isSubmitting}>
                  {isSubmitting ? 'Enviando...' : 'Enviar enlace'}
                </Button>
              </Box>
            </>
          )}

          <Typography variant="body2" textAlign="center" color="text.secondary" mt={3}>
            <Link to="/login" style={{ color: '#2563EB' }}>← Volver al login</Link>
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}
