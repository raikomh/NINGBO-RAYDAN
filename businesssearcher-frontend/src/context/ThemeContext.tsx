import React, { createContext, useContext, useState, useMemo } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';

type Mode = 'light' | 'dark';

interface ThemeContextValue {
  mode: Mode;
  toggleMode: () => void;
}

const ThemeContext = createContext<ThemeContextValue>({ mode: 'light', toggleMode: () => {} });

export function useThemeMode() {
  return useContext(ThemeContext);
}

function buildTheme(mode: Mode) {
  const isDark = mode === 'dark';
  return createTheme({
    palette: {
      mode,
      primary: { main: '#2563EB', light: '#3B82F6', dark: '#1D4ED8' },
      secondary: { main: '#7C3AED' },
      background: {
        default: isDark ? '#030508' : '#F1F5FB',
        paper: isDark ? '#080D14' : '#FFFFFF',
      },
      text: {
        primary: isDark ? '#E2E8F0' : '#0F172A',
        secondary: isDark ? '#7A8FA8' : '#64748B',
      },
      success: { main: '#10B981' },
      error: { main: '#EF4444' },
      warning: { main: '#F59E0B' },
      divider: isDark ? '#1E2D45' : '#E2E8F0',
    },
    typography: {
      fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
      h1: { fontWeight: 700 }, h2: { fontWeight: 700 },
      h3: { fontWeight: 600 }, h4: { fontWeight: 600 },
      h5: { fontWeight: 600 }, h6: { fontWeight: 600 },
    },
    shape: { borderRadius: 12 },
    components: {
      MuiButton: {
        styleOverrides: {
          root: {
            textTransform: 'none',
            fontWeight: 600,
            borderRadius: 10,
            transition: 'transform 0.18s ease, box-shadow 0.18s ease, background-color 0.18s ease',
            '&:hover': {
              transform: 'translateY(-2px)',
              boxShadow: '0 6px 20px -4px rgb(37 99 235 / 0.35)',
            },
            '&:active': {
              transform: 'translateY(0px)',
            },
          },
          contained: {
            boxShadow: '0 2px 8px -2px rgb(37 99 235 / 0.25)',
            '&:hover': {
              boxShadow: '0 8px 24px -4px rgb(37 99 235 / 0.4)',
            },
          },
          outlined: {
            '&:hover': {
              boxShadow: '0 4px 12px -4px rgb(37 99 235 / 0.2)',
            },
          },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            transition: 'transform 0.18s ease, background-color 0.2s ease',
            '&:hover': { transform: 'scale(1.12)' },
            '&:active': { transform: 'scale(0.95)' },
          },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            boxShadow: isDark
              ? '0 4px 20px 0 rgb(0 0 0 / 0.6), 0 1px 4px 0 rgb(0 0 0 / 0.4)'
              : '0 2px 8px -2px rgb(0 0 0 / 0.08), 0 1px 2px -1px rgb(0 0 0 / 0.06)',
            borderRadius: 16,
            border: isDark
              ? '1px solid rgb(30 50 80 / 0.9)'
              : '1px solid rgb(226 232 240 / 0.8)',
            backgroundColor: isDark ? '#0C1220' : undefined,
            transition: 'transform 0.22s ease, box-shadow 0.22s ease, border-color 0.22s ease',
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            fontWeight: 500,
            transition: 'transform 0.15s ease',
            '&:hover': { transform: 'scale(1.04)' },
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            ...(isDark && { backgroundColor: '#0C1220' }),
          },
        },
      },
      MuiTextField: { defaultProps: { variant: 'outlined', size: 'small' } },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            transition: 'background-color 0.2s ease, transform 0.15s ease',
            '&:active': { transform: 'scale(0.98)' },
          },
        },
      },
    },
  });
}

function applyDarkClass(mode: Mode) {
  document.documentElement.classList.toggle('dark', mode === 'dark');
}

export function AppThemeProvider({ children }: { children: React.ReactNode }) {
  const stored = (localStorage.getItem('theme-mode') as Mode) ?? 'light';
  const [mode, setMode] = useState<Mode>(() => {
    applyDarkClass(stored);
    return stored;
  });

  const toggleMode = () => {
    setMode((prev) => {
      const next = prev === 'light' ? 'dark' : 'light';
      localStorage.setItem('theme-mode', next);
      applyDarkClass(next);
      return next;
    });
  };

  const theme = useMemo(() => buildTheme(mode), [mode]);

  return (
    <ThemeContext.Provider value={{ mode, toggleMode }}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeContext.Provider>
  );
}
