import { useState, useRef, useEffect, useCallback } from 'react';
import {
  Box, Fab, Paper, Typography, TextField, IconButton,
  Avatar, Slide, Badge, CircularProgress, Tooltip,
} from '@mui/material';
import {
  SupportAgent as SupportIcon,
  Send as SendIcon,
  Close as CloseIcon,
  ChatBubble as ChatIcon,
  DeleteOutline as ClearIcon,
} from '@mui/icons-material';
import { api } from '@/lib/apiClient';

interface Message {
  id: string;
  role: 'user' | 'support';
  content: string;
}

const WELCOME: Message = {
  id: 'welcome',
  role: 'support',
  content: '¡Hola! Escribe tu consulta y el equipo de soporte de MerkaCuba te responderá por aquí.',
};

export default function ChatWidget() {
  const [open, setOpen]               = useState(false);
  const [messages, setMessages]       = useState<Message[]>([WELCOME]);
  const [input, setInput]             = useState('');
  const [loading, setLoading]         = useState(false);
  const [unread, setUnread]           = useState(0);
  const [historyLoaded, setHistoryLoaded] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);
  const inputRef  = useRef<HTMLInputElement>(null);

  const loadHistory = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/chat/messages');
      const payload = res.data?.data ?? res.data;
      const items: Array<{ id: string; text?: string; senderType?: string; isFromAdmin?: boolean }> =
        payload?.items ?? (Array.isArray(payload) ? payload : []);
      if (items.length > 0) {
        const mapped: Message[] = items.map((m) => ({
          id: m.id,
          role: m.senderType === 'Admin' || m.isFromAdmin ? 'support' : 'user',
          content: m.text ?? '',
        }));
        setMessages([WELCOME, ...mapped]);
      }
    } catch {
      // keep welcome message
    } finally {
      setHistoryLoaded(true);
    }
  }, []);

  useEffect(() => {
    if (open) {
      setUnread(0);
      api.patch('/api/v1/chat/messages/read').catch(() => {});
      setTimeout(() => inputRef.current?.focus(), 150);
      if (!historyLoaded) loadHistory();
    }
  }, [open, historyLoaded, loadHistory]);

  // Poll unread count while widget is closed
  useEffect(() => {
    if (open) return;
    const fetchUnread = () => {
      api.get('/api/v1/chat/unread')
        .then((res) => setUnread(res.data?.data?.count ?? res.data?.count ?? 0))
        .catch(() => {});
    };
    fetchUnread();
    const id = setInterval(fetchUnread, 30_000);
    return () => clearInterval(id);
  }, [open]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const sendMessage = async () => {
    const text = input.trim();
    if (!text || loading) return;

    const userMsg: Message = { id: Date.now().toString(), role: 'user', content: text };
    setMessages((prev) => [...prev, userMsg]);
    setInput('');
    setLoading(true);

    try {
      await api.post('/api/v1/chat/messages', { text });
    } catch {
      setMessages((prev) => [
        ...prev,
        { id: `${Date.now()}-err`, role: 'support', content: 'No se pudo enviar tu mensaje. Inténtalo de nuevo.' },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(); }
  };

  const clearChat = () => setMessages([WELCOME]);

  return (
    <>
      <Slide direction="up" in={open} mountOnEnter unmountOnExit>
        <Paper
          elevation={8}
          sx={{
            position: 'fixed',
            bottom: { xs: 80, sm: 96 },
            right: { xs: 12, sm: 24 },
            width: { xs: 'calc(100vw - 24px)', sm: 380 },
            height: { xs: 'calc(100vh - 120px)', sm: 520 },
            maxHeight: 520,
            display: 'flex', flexDirection: 'column',
            borderRadius: 3, overflow: 'hidden', zIndex: 1300,
            border: '1px solid', borderColor: 'divider',
          }}
        >
          {/* Header */}
          <Box
            sx={{
              px: 2, py: 1.5,
              display: 'flex', alignItems: 'center', gap: 1.5,
              background: 'linear-gradient(135deg, #2563EB 0%, #1D4ED8 100%)',
              color: 'white', flexShrink: 0,
            }}
          >
            <Avatar sx={{ width: 32, height: 32, bgcolor: 'rgba(255,255,255,0.2)' }}>
              <SupportIcon sx={{ fontSize: 18 }} />
            </Avatar>
            <Box flex={1}>
              <Typography fontWeight={700} fontSize="0.95rem" lineHeight={1.2}>
                Soporte
              </Typography>
            </Box>
            <Tooltip title="Limpiar conversación">
              <IconButton size="small" onClick={clearChat} sx={{ color: 'rgba(255,255,255,0.8)', '&:hover': { color: 'white', bgcolor: 'rgba(255,255,255,0.1)' } }}>
                <ClearIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <IconButton size="small" onClick={() => setOpen(false)} sx={{ color: 'rgba(255,255,255,0.8)', '&:hover': { color: 'white', bgcolor: 'rgba(255,255,255,0.1)' } }}>
              <CloseIcon fontSize="small" />
            </IconButton>
          </Box>

          {/* Messages */}
          <Box
            sx={{
              flex: 1, overflowY: 'auto', px: 2, py: 1.5,
              display: 'flex', flexDirection: 'column', gap: 1.5,
              bgcolor: 'background.default',
              '&::-webkit-scrollbar': { width: 4 },
              '&::-webkit-scrollbar-thumb': { bgcolor: 'divider', borderRadius: 2 },
            }}
          >
            {messages.map((msg) => (
              <Box
                key={msg.id}
                display="flex"
                justifyContent={msg.role === 'user' ? 'flex-end' : 'flex-start'}
                alignItems="flex-end"
                gap={1}
              >
                {msg.role === 'support' && (
                  <Avatar sx={{ width: 26, height: 26, bgcolor: 'primary.main', flexShrink: 0, mb: 0.25 }}>
                    <SupportIcon sx={{ fontSize: 14 }} />
                  </Avatar>
                )}
                <Box
                  sx={{
                    maxWidth: '78%', px: 1.5, py: 1,
                    borderRadius: msg.role === 'user' ? '16px 16px 4px 16px' : '16px 16px 16px 4px',
                    bgcolor: msg.role === 'user' ? 'primary.main' : 'background.paper',
                    color: msg.role === 'user' ? 'white' : 'text.primary',
                    boxShadow: '0 1px 4px rgb(0 0 0 / 0.08)',
                    border: msg.role === 'support' ? '1px solid' : 'none',
                    borderColor: 'divider',
                  }}
                >
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', lineHeight: 1.55, fontSize: '0.875rem' }}>
                    {msg.content}
                  </Typography>
                </Box>
              </Box>
            ))}

            <div ref={bottomRef} />
          </Box>

          {/* Input */}
          <Box sx={{ px: 1.5, py: 1.25, display: 'flex', alignItems: 'flex-end', gap: 1, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.paper', flexShrink: 0 }}>
            <TextField
              inputRef={inputRef}
              fullWidth multiline maxRows={4} size="small"
              placeholder="Escribe tu mensaje…"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              disabled={loading}
              sx={{ '& .MuiOutlinedInput-root': { borderRadius: 3, fontSize: '0.875rem' } }}
            />
            <IconButton
              onClick={sendMessage}
              disabled={!input.trim() || loading}
              sx={{ bgcolor: 'primary.main', color: 'white', width: 38, height: 38, flexShrink: 0, transition: 'transform 0.15s ease, background-color 0.15s ease', '&:hover': { bgcolor: 'primary.dark', transform: 'scale(1.08)' }, '&.Mui-disabled': { bgcolor: 'action.disabledBackground', color: 'action.disabled' } }}
            >
              {loading ? <CircularProgress size={16} color="inherit" /> : <SendIcon sx={{ fontSize: 18 }} />}
            </IconButton>
          </Box>
        </Paper>
      </Slide>

      <Tooltip title={open ? '' : 'Soporte'} placement="left">
        <Badge badgeContent={unread} color="error" overlap="circular">
          <Fab
            color="primary"
            onClick={() => setOpen((v) => !v)}
            sx={{
              position: 'fixed', bottom: { xs: 16, sm: 24 }, right: { xs: 16, sm: 24 }, zIndex: 1301,
              boxShadow: '0 6px 24px -4px rgb(37 99 235 / 0.5)',
              transition: 'transform 0.22s ease, box-shadow 0.22s ease',
              '&:hover': { transform: 'scale(1.1)', boxShadow: '0 10px 32px -4px rgb(37 99 235 / 0.6)' },
              '&:active': { transform: 'scale(0.95)' },
            }}
          >
            {open ? <CloseIcon /> : <ChatIcon />}
          </Fab>
        </Badge>
      </Tooltip>
    </>
  );
}
