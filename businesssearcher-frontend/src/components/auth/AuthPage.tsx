import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  TextField, Button, Alert, InputAdornment, IconButton, CircularProgress, Box, Typography,
  ToggleButtonGroup, ToggleButton,
} from '@mui/material';
import {
  Visibility, VisibilityOff, StorefrontOutlined, AnalyticsOutlined, ShoppingCartOutlined,
  CheckCircleOutline, MarkEmailReadOutlined, Storefront, LocalShipping,
} from '@mui/icons-material';
import { useAuth, AUTH_NOTICE_KEY } from '@/context/AuthContext';
import { api } from '@/lib/apiClient';
import { isLocalDeployment } from '@/lib/deployment';

export type AuthMode = 'login' | 'register';

// Formato mínimo (algo@dominio.tld) sin restringir a ASCII: hay cuentas con ñ/acentos en el usuario
// del correo (p. ej. cutiño@...) que z.string().email() y el type="email" nativo rechazarían.
const loginSchema = z.object({
  email: z.string().trim().refine((v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), 'Email inválido'),
  password: z.string().min(1, 'La contraseña es requerida'),
});
type LoginFormData = z.infer<typeof loginSchema>;

const registerSchema = z.object({
  businessName: z.string().min(2, 'Mínimo 2 caracteres'),
  email: z.string().email('Email inválido'),
  password: z.string().min(8, 'Mínimo 8 caracteres'),
  confirmPassword: z.string(),
  tenantType: z.enum(['Retail', 'Wholesale'], { required_error: 'Selecciona un tipo de negocio' }),
  referralCode: z.string().optional(),
}).refine((d) => d.password === d.confirmPassword, {
  message: 'Las contraseñas no coinciden',
  path: ['confirmPassword'],
});
type RegisterFormData = z.infer<typeof registerSchema>;

const loginFeatures = [
  { icon: <StorefrontOutlined fontSize="small" />, text: 'Gestiona tus tiendas y productos en un solo lugar' },
  { icon: <AnalyticsOutlined fontSize="small" />, text: 'Analítica en tiempo real y reportes mensuales' },
  { icon: <ShoppingCartOutlined fontSize="small" />, text: 'Control de ventas, entradas y mermas' },
];

const registerBenefits = [
  'Gestiona tu tienda en el plan gratuito',
  'Analíticas y reportes en tiempo real',
  'Control de inventario y transacciones',
  'Sin necesidad de tarjeta de crédito',
];

function syncUrl(mode: AuthMode) {
  const path = mode === 'login' ? '/login' : '/register';
  if (window.location.pathname !== path) window.history.replaceState(null, '', path);
}

export default function AuthPage({ initialMode }: { initialMode: AuthMode }) {
  const [mode, setMode] = useState<AuthMode>(initialMode);
  const goTo = (next: AuthMode) => { setMode(next); syncUrl(next); };

  return (
    <div className="auth-viewport">
      <div className="auth-bg" data-mode={mode} />
      <div className="auth-track" data-mode={mode}>
        <div className="auth-slide" aria-hidden={mode !== 'login'}>
          <LoginPanel active={mode === 'login'} onSwitch={() => goTo('register')} />
        </div>
        <div className="auth-slide" aria-hidden={mode !== 'register'}>
          <RegisterPanel active={mode === 'register'} onSwitch={() => goTo('login')} />
        </div>
      </div>
    </div>
  );
}

function LoginPanel({ active, onSwitch }: { active: boolean; onSwitch: () => void }) {
  const { login, loginOps } = useAuth();
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [needsVerification, setNeedsVerification] = useState(false);
  const [resendState, setResendState] = useState<'idle' | 'sending' | 'sent'>('idle');
  const [employeeMode, setEmployeeMode] = useState(false);
  const [notice, setNotice] = useState('');

  // Aviso de un logout forzado por suscripción vencida (ver AuthContext.forceLogoutForExpiredSubscription).
  useEffect(() => {
    const stored = localStorage.getItem(AUTH_NOTICE_KEY);
    if (stored) {
      setNotice(stored);
      localStorage.removeItem(AUTH_NOTICE_KEY);
    }
  }, []);

  const { register, handleSubmit, getValues, formState: { errors, isSubmitting } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (data: LoginFormData) => {
    setError('');
    setNeedsVerification(false);
    setResendState('idle');
    try {
      // El password no se recorta en el schema (a diferencia del email) para no alterar lo que
      // el usuario ve mientras escribe; se recorta solo aquí, al enviar, porque un espacio final
      // invisible (típico de autocompletado/copy-paste) rompe la verificación de hash en silencio
      // y el usuario ve "Credenciales inválidas" sin entender por qué.
      const password = data.password.trim();
      if (employeeMode) {
        await loginOps(data.email, password);
        navigate('/dashboard/select-store?next=' + encodeURIComponent('/dashboard/ops/pos'));
      } else {
        await login(data.email, password);
        navigate('/dashboard/select-store?next=' + encodeURIComponent('/dashboard'));
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? 'Credenciales incorrectas. Inténtalo de nuevo.');
      setNeedsVerification(!employeeMode && !!msg && msg.toLowerCase().includes('verificar'));
    }
  };

  const handleResendVerification = async () => {
    setResendState('sending');
    try {
      await api.post('/api/v1/auth/resend-verification', { email: getValues('email') });
      setResendState('sent');
    } catch {
      setResendState('idle');
      setError('No se pudo reenviar el correo. Inténtalo de nuevo en unos minutos.');
    }
  };

  return (
    <div className="flex min-h-screen w-full">
      {/* Panel izquierdo — degradado azul marino a azul claro */}
      <div className="hidden lg:flex flex-col justify-between text-white p-10 lg:w-[45%] xl:w-[42%] flex-shrink-0">
        <div>
          <span className="text-xl font-extrabold tracking-tight">MerkaCuba</span>
          <p className="text-blue-200 text-sm mt-1">Gestión de negocios simplificada</p>
        </div>

        <div className="space-y-8">
          <div className="relative mb-6">
            <div className="absolute -top-6 -left-4 w-32 h-32 bg-white/5 rounded-full blur-2xl" />
            <div className="absolute top-8 left-20 w-20 h-20 bg-sky-300/20 rounded-full blur-xl" />
          </div>

          <div>
            <h2 className="text-4xl font-extrabold leading-tight mb-4">
              Todo lo que tu negocio necesita
            </h2>
            <p className="text-blue-100 text-base leading-relaxed">
              Administra tus tiendas, productos y clientes desde un dashboard centralizado.
            </p>
          </div>

          <ul className="space-y-4">
            {loginFeatures.map((f, i) => (
              <li key={i} className="flex items-center gap-3">
                <span className="w-9 h-9 rounded-xl bg-white/15 flex items-center justify-center flex-shrink-0 backdrop-blur-sm">
                  {f.icon}
                </span>
                <span className="text-blue-50 text-sm leading-snug">{f.text}</span>
              </li>
            ))}
          </ul>
        </div>

        <p className="text-blue-300/70 text-xs">© {new Date().getFullYear()} MerkaCuba. Todos los derechos reservados.</p>
      </div>

      {/* Formulario */}
      <div className="flex-1 flex items-center justify-center p-6 lg:p-14 bg-white dark:bg-gray-950">
        <div className={`w-full max-w-md transition-opacity duration-700 ${active ? 'opacity-100' : 'opacity-50'}`}>
          <div className="lg:hidden mb-8 text-center">
            <span className="text-2xl font-extrabold text-blue-600 tracking-tight">MerkaCuba</span>
          </div>

          <div className="mb-6">
            <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white tracking-tight mb-2">
              Bienvenido de vuelta
            </h1>
            <p className="text-gray-500 dark:text-gray-400 text-sm">
              {employeeMode ? 'Acceso de empleado (Punto de Venta)' : 'Inicia sesión en tu cuenta para continuar'}
            </p>
          </div>

          <div className="mb-6 inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-1 bg-gray-50 dark:bg-gray-900">
            <button type="button" onClick={() => { setEmployeeMode(false); setError(''); }}
              className={`px-4 py-1.5 text-sm font-semibold rounded-md transition-colors ${!employeeMode ? 'bg-blue-600 text-white' : 'text-gray-500'}`}>
              Dueño
            </button>
            <button type="button" onClick={() => { setEmployeeMode(true); setError(''); }}
              className={`px-4 py-1.5 text-sm font-semibold rounded-md transition-colors ${employeeMode ? 'bg-blue-600 text-white' : 'text-gray-500'}`}>
              Empleado (TPV)
            </button>
          </div>

          {notice && <Alert severity="info" className="mb-4" onClose={() => setNotice('')}>{notice}</Alert>}
          {error && <Alert severity="error" className="mb-4">{error}</Alert>}

          {needsVerification && resendState !== 'sent' && (
            <Button
              variant="outlined"
              fullWidth
              disabled={resendState === 'sending'}
              onClick={handleResendVerification}
              sx={{ mb: 2, textTransform: 'none', fontWeight: 600 }}
            >
              {resendState === 'sending'
                ? <CircularProgress size={20} color="inherit" />
                : '¿No te llegó el correo? Reenviar verificación'}
            </Button>
          )}
          {resendState === 'sent' && (
            <Alert severity="success" className="mb-4">
              Correo de verificación reenviado. Revisa tu bandeja de entrada (y la carpeta de spam).
            </Alert>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
            <TextField
              label="Correo electrónico"
              type="email"
              fullWidth
              variant="outlined"
              {...register('email')}
              error={!!errors.email}
              helperText={errors.email?.message}
              autoComplete="email"
            />
            <TextField
              label="Contraseña"
              type={showPassword ? 'text' : 'password'}
              fullWidth
              variant="outlined"
              {...register('password')}
              error={!!errors.password}
              helperText={errors.password?.message}
              autoComplete="current-password"
              InputProps={{
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton onClick={() => setShowPassword((p) => !p)} edge="end" tabIndex={-1}>
                      {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                    </IconButton>
                  </InputAdornment>
                ),
              }}
            />

            <div className="flex justify-end -mt-1">
              <Link to="/forgot-password" className="text-xs text-blue-600 hover:text-blue-700 font-medium transition-colors">
                ¿Olvidaste tu contraseña?
              </Link>
            </div>

            <div className="glow-btn">
              <Button
                type="submit"
                variant="contained"
                fullWidth
                size="large"
                disabled={isSubmitting}
                sx={{ py: 1.5, fontWeight: 700, borderRadius: '999px', textTransform: 'none', fontSize: '1rem' }}
              >
                {isSubmitting ? <CircularProgress size={22} color="inherit" /> : 'Iniciar sesión'}
              </Button>
            </div>
          </form>

          <div className="mt-6 text-center">
            <span className="text-gray-500 dark:text-gray-400 text-sm">¿No tienes una cuenta?{' '}</span>
            <button
              type="button"
              onClick={onSwitch}
              className="text-blue-600 font-semibold text-sm hover:text-blue-700 transition-colors"
            >
              Regístrate gratis
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function RegisterPanel({ active, onSwitch }: { active: boolean; onSwitch: () => void }) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [registered, setRegistered] = useState(false);

  const { register, handleSubmit, control, formState: { errors, isSubmitting } } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: { tenantType: 'Retail', referralCode: searchParams.get('ref') ?? '' },
  });

  const onSubmit = async (data: RegisterFormData) => {
    setError('');
    try {
      // El registro ya NO inicia sesión: la cuenta queda pendiente de verificar
      // el email y de ser aprobada por un administrador antes de poder loguearse.
      await api.post('/api/v1/auth/register', {
        businessName: data.businessName,
        email: data.email,
        password: data.password,
        tenantType: data.tenantType,
        referralCode: data.referralCode || undefined,
      });
      setRegistered(true);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? 'Error al registrarse. Inténtalo de nuevo.');
    }
  };

  if (registered) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" justifyContent="center" minHeight="100vh" gap={3} px={2} textAlign="center" className="bg-white dark:bg-gray-950">
        <MarkEmailReadOutlined sx={{ fontSize: 64, color: 'success.main' }} />
        <Typography variant="h5" fontWeight={700}>¡Cuenta creada!</Typography>
        {isLocalDeployment ? (
          <>
            <Typography variant="body1" color="text.secondary" maxWidth={440}>
              Tu instalación ya está activa — puedes iniciar sesión ahora mismo y empezar a trabajar,
              sin esperar nada.
            </Typography>
            <Alert severity="info" sx={{ maxWidth: 440, textAlign: 'left' }}>
              De paso estamos conectando esta instalación con tu cuenta en línea, en segundo plano.
              Mientras se aprueba allá, sigues trabajando normal — cuando quede lista, se sincroniza
              sola. Puedes ver el estado en Sincronización.
            </Alert>
          </>
        ) : (
          <Typography variant="body1" color="text.secondary" maxWidth={420}>
            Revisa tu correo para verificar tu email. Después, un administrador debe aprobar
            tu cuenta antes de que puedas iniciar sesión — te avisaremos cuando esté lista.
          </Typography>
        )}
        <Button variant="contained" size="large" onClick={() => navigate('/login')}>
          Ir al inicio de sesión
        </Button>
      </Box>
    );
  }

  return (
    <div className="flex min-h-screen w-full">
      {/* Formulario */}
      <div className="flex-1 flex items-center justify-center p-6 lg:p-14 bg-white dark:bg-gray-950 order-2 lg:order-1">
        <div className={`w-full max-w-md transition-opacity duration-700 ${active ? 'opacity-100' : 'opacity-50'}`}>
          <div className="lg:hidden mb-8 text-center">
            <span className="text-2xl font-extrabold text-amber-600 tracking-tight">MerkaCuba</span>
          </div>

          <div className="mb-7">
            <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white tracking-tight mb-2">
              Crea tu cuenta
            </h1>
            <p className="text-gray-500 dark:text-gray-400 text-sm">
              Empieza a gestionar tu negocio gratis — sin tarjeta
            </p>
          </div>

          {error && <Alert severity="error" className="mb-4">{error}</Alert>}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <Box>
              <Typography variant="body2" fontWeight={500} mb={1} color="text.secondary">
                Tipo de negocio
              </Typography>
              <Controller
                name="tenantType"
                control={control}
                render={({ field }) => (
                  <ToggleButtonGroup
                    exclusive
                    fullWidth
                    color="warning"
                    value={field.value}
                    onChange={(_, value) => value && field.onChange(value)}
                  >
                    <ToggleButton value="Retail" sx={{ textTransform: 'none', py: 1.25, gap: 1 }}>
                      <Storefront fontSize="small" />
                      Minorista
                    </ToggleButton>
                    <ToggleButton value="Wholesale" sx={{ textTransform: 'none', py: 1.25, gap: 1 }}>
                      <LocalShipping fontSize="small" />
                      Mayorista
                    </ToggleButton>
                  </ToggleButtonGroup>
                )}
              />
              {errors.tenantType && (
                <Typography variant="caption" color="error" sx={{ mt: 0.5, display: 'block' }}>
                  {errors.tenantType.message}
                </Typography>
              )}
            </Box>
            <TextField
              label="Nombre del negocio"
              fullWidth
              {...register('businessName')}
              error={!!errors.businessName}
              helperText={errors.businessName?.message}
              autoComplete="organization"
            />
            <TextField
              label="Correo electrónico"
              type="email"
              fullWidth
              {...register('email')}
              error={!!errors.email}
              helperText={errors.email?.message}
              autoComplete="email"
            />
            <TextField
              label="Contraseña"
              type={showPassword ? 'text' : 'password'}
              fullWidth
              {...register('password')}
              error={!!errors.password}
              helperText={errors.password?.message}
              autoComplete="new-password"
              InputProps={{
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton onClick={() => setShowPassword((p) => !p)} edge="end" tabIndex={-1}>
                      {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                    </IconButton>
                  </InputAdornment>
                ),
              }}
            />
            <TextField
              label="Confirmar contraseña"
              type={showPassword ? 'text' : 'password'}
              fullWidth
              {...register('confirmPassword')}
              error={!!errors.confirmPassword}
              helperText={errors.confirmPassword?.message}
              autoComplete="new-password"
            />
            <TextField
              label="Código de referido (opcional)"
              fullWidth
              {...register('referralCode')}
              error={!!errors.referralCode}
              helperText={errors.referralCode?.message ?? 'Si un cliente te invitó a registrar tu negocio, ingresa su código'}
              autoComplete="off"
            />

            <div className="glow-btn glow-btn--amber" style={{ marginTop: 8 }}>
              <Button
                type="submit"
                variant="contained"
                color="warning"
                fullWidth
                size="large"
                disabled={isSubmitting}
                sx={{ py: 1.5, fontWeight: 700, borderRadius: '999px', textTransform: 'none', fontSize: '1rem' }}
              >
                {isSubmitting ? <CircularProgress size={22} color="inherit" /> : 'Crear cuenta gratis'}
              </Button>
            </div>
          </form>

          <div className="mt-5 text-center">
            <span className="text-gray-500 dark:text-gray-400 text-sm">¿Ya tienes una cuenta?{' '}</span>
            <button
              type="button"
              onClick={onSwitch}
              className="text-amber-600 font-semibold text-sm hover:text-amber-700 transition-colors"
            >
              Inicia sesión
            </button>
          </div>
        </div>
      </div>

      {/* Panel derecho — degradado naranja/ámbar a amarillo claro */}
      <div className="hidden lg:flex flex-col justify-between text-white p-10 lg:w-[42%] flex-shrink-0 order-1 lg:order-2">
        <div>
          <span className="text-xl font-extrabold tracking-tight">MerkaCuba</span>
          <p className="text-amber-100 text-sm mt-1">Gestión de negocios simplificada</p>
        </div>

        <div className="space-y-6">
          <div>
            <h2 className="text-4xl font-extrabold leading-tight mb-3">
              Comienza gratis hoy
            </h2>
            <p className="text-amber-50 text-base leading-relaxed">
              Únete a miles de negocios que ya gestionan sus operaciones con MerkaCuba.
            </p>
          </div>

          <ul className="space-y-3">
            {registerBenefits.map((b, i) => (
              <li key={i} className="flex items-center gap-3">
                <CheckCircleOutline className="text-amber-100 flex-shrink-0" sx={{ fontSize: 20 }} />
                <span className="text-amber-50 text-sm">{b}</span>
              </li>
            ))}
          </ul>
        </div>

        <p className="text-amber-100/70 text-xs">© {new Date().getFullYear()} MerkaCuba. Todos los derechos reservados.</p>
      </div>
    </div>
  );
}
