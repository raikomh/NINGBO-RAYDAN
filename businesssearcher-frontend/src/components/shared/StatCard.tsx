import { Card, CardContent, Typography, Box, Chip } from '@mui/material';
import { TrendingUp, TrendingDown } from '@mui/icons-material';
import type { SvgIconComponent } from '@mui/icons-material';
import { SPRING } from '@/lib/motion';

interface StatCardProps {
  title: string;
  value: string | number;
  icon: React.ReactElement<SvgIconComponent>;
  color?: string;
  subtitle?: string;
  onClick?: () => void;
  trend?: number;
}

export default function StatCard({ title, value, icon, color = '#2563EB', subtitle, onClick, trend }: StatCardProps) {
  return (
    <Card
      onClick={onClick}
      sx={{
        cursor: onClick ? 'pointer' : 'default',
        border: '1px solid rgba(0,0,0,0.08)',
        borderLeft: `4px solid ${color}`,
        transition: `transform 0.30s ${SPRING}, box-shadow 0.25s ease, border-color 0.2s ease`,
        '&:hover': {
          transform: 'translateY(-12px)',
          boxShadow: `0 24px 48px ${color}99, 0 6px 12px rgba(0,0,0,0.08)`,
          borderColor: `${color}`,
        },
      }}
    >
      <CardContent sx={{ p: 2.5, '&:last-child': { pb: 2.5 } }}>
        <Box display="flex" alignItems="flex-start" justifyContent="space-between">
          <Box flex={1} minWidth={0}>
            <Typography
              variant="caption"
              color="text.secondary"
              fontWeight={600}
              sx={{ textTransform: 'uppercase', letterSpacing: '0.06em', fontSize: '0.68rem' }}
            >
              {title}
            </Typography>
            <Box display="flex" alignItems="baseline" gap={1} mt={0.5}>
              <Typography variant="h4" fontWeight={800} sx={{ lineHeight: 1.1 }}>
                {value}
              </Typography>
              {trend !== undefined && (
                <Chip
                  size="small"
                  icon={trend >= 0
                    ? <TrendingUp sx={{ fontSize: '14px !important' }} />
                    : <TrendingDown sx={{ fontSize: '14px !important' }} />}
                  label={`${trend >= 0 ? '+' : ''}${trend}%`}
                  sx={{
                    height: 20,
                    fontSize: '0.65rem',
                    fontWeight: 700,
                    bgcolor: trend >= 0 ? '#10B98115' : '#EF444415',
                    color: trend >= 0 ? '#10B981' : '#EF4444',
                    border: 'none',
                    '& .MuiChip-icon': { ml: 0.5 },
                  }}
                />
              )}
            </Box>
            {subtitle && (
              <Typography
                variant="caption"
                color={onClick ? 'primary' : 'text.secondary'}
                sx={{ mt: 0.5, display: 'block', fontWeight: onClick ? 600 : 400 }}
              >
                {subtitle}
              </Typography>
            )}
          </Box>

          <Box
            sx={{
              p: 1.5,
              borderRadius: 3,
              background: `linear-gradient(135deg, ${color}22 0%, ${color}10 100%)`,
              border: `1px solid ${color}25`,
              color: color,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
              transition: `transform 0.4s ${SPRING}, box-shadow 0.3s ease-out, background 0.25s ease-out`,
              '.MuiCard-root:hover &': {
                transform: 'rotate(-10deg) scale(1.22)',
                boxShadow: `0 10px 28px -4px ${color}55`,
                background: `linear-gradient(135deg, ${color}40 0%, ${color}20 100%)`,
              },
              '& svg': { fontSize: 26 },
            }}
          >
            {icon}
          </Box>
        </Box>

        <Box
          sx={{
            mt: 2,
            height: 3,
            borderRadius: 99,
            background: `linear-gradient(90deg, ${color}60 0%, ${color}15 100%)`,
            opacity: 0.7,
          }}
        />
      </CardContent>
    </Card>
  );
}
