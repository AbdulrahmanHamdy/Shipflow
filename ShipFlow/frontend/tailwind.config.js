/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        forest: {
          night: '#0B2A1F',
          deep: '#123B2B',
          surface: '#154131',
          border: '#1C543F'
        },
        brand: {
          DEFAULT: '#15803D',
          hover: '#166534',
          accent: '#10B981',
          mint: '#DCFCE7',
          mintText: '#14532D',
          tint: '#F0FDF4'
        },
        neutral: {
          appBg: '#F4F8F5',
          surface: '#FFFFFF',
          primary: '#13211A',
          secondary: '#5B6B62',
          muted: '#8D9E94',
          border: '#DCE7E0',
          hover: '#EBF2EE'
        },
        status: {
          pending: '#F59E0B',
          quoting: '#8B5CF6',
          accepted: '#0284C7',
          assigned: '#0D9488',
          pickedUp: '#2563EB',
          inTransit: '#0284C7',
          delivered: '#15803D',
          deliveryFailed: '#DC2626',
          cancelled: '#6B7280'
        }
      },
      fontFamily: {
        sans: [
          'Inter',
          '-apple-system',
          'BlinkMacSystemFont',
          '"Segoe UI"',
          'Roboto',
          'sans-serif'
        ],
        mono: [
          'ui-monospace',
          'SFMono-Regular',
          'Menlo',
          'Monaco',
          'Consolas',
          'monospace'
        ]
      },
      boxShadow: {
        card: '0 1px 3px rgba(11, 42, 31, 0.05), 0 1px 2px rgba(11, 42, 31, 0.03)',
        cardHover: '0 4px 6px -1px rgba(11, 42, 31, 0.08), 0 2px 4px -2px rgba(11, 42, 31, 0.06)',
        panel: '0 10px 15px -3px rgba(11, 42, 31, 0.08), 0 4px 6px -4px rgba(11, 42, 31, 0.04)',
        glow: '0 0 15px rgba(16, 185, 129, 0.25)'
      }
    },
  },
  plugins: [],
}
