import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import { captureMarketingSource } from '@/lib/marketing';

captureMarketingSource();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
