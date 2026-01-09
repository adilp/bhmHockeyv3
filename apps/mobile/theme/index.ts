/**
 * BHM Hockey Design System
 * Sleeper-inspired dark theme
 */

export const colors = {
  // Primary Accent Colors
  primary: {
    teal: '#00D9C0',
    tealLight: '#5EEAD4',
    green: '#3FB950',
    greenLight: '#56D364',
    purple: '#A371F7',
    purpleLight: '#BC8CFF',
    blue: '#58A6FF',
  },

  // Background Colors - Dark Navy Spectrum
  bg: {
    darkest: '#0D1117',
    dark: '#161B22',
    elevated: '#1C2128',
    hover: '#21262D',
    active: '#282E36',
  },

  // Text Colors
  text: {
    primary: '#FFFFFF',
    secondary: '#C9D1D9',
    muted: '#8B949E',
    subtle: '#6E7681',
    placeholder: '#484F58',
  },

  // Border Colors
  border: {
    default: '#21262D',
    muted: '#30363D',
    emphasis: '#484F58',
  },

  // Status Colors
  status: {
    success: '#3FB950',
    successSubtle: 'rgba(63, 185, 80, 0.15)',
    warning: '#D29922',
    warningSubtle: 'rgba(210, 153, 34, 0.15)',
    error: '#F85149',
    errorSubtle: 'rgba(248, 81, 73, 0.15)',
    info: '#58A6FF',
    infoSubtle: 'rgba(88, 166, 255, 0.15)',
  },

  // Accent Subtle Backgrounds
  subtle: {
    teal: 'rgba(0, 217, 192, 0.12)',
    green: 'rgba(63, 185, 80, 0.12)',
    purple: 'rgba(163, 113, 247, 0.12)',
    blue: 'rgba(88, 166, 255, 0.12)',
  },

  // Skill Level Colors - Very subtle versions
  skillLevel: {
    Gold: '#5C4D3C',      // Very muted gold
    Silver: '#4A4F57',    // Very muted silver
    Bronze: '#4D3D2E',    // Very muted bronze
    'D-League': '#463D54', // Very muted purple
  },
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
} as const;

export const radius = {
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
  round: 9999,
} as const;

export const typography = {
  // Font weights
  weight: {
    regular: '400' as const,
    medium: '500' as const,
    semibold: '600' as const,
    bold: '700' as const,
  },

  // Common text styles
  screenTitle: {
    fontSize: 28,
    fontWeight: '700' as const,
    color: colors.text.primary,
    letterSpacing: -0.5,
  },
  screenSubtitle: {
    fontSize: 14,
    color: colors.text.muted,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600' as const,
    color: colors.text.muted,
    textTransform: 'uppercase' as const,
    letterSpacing: 0.5,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '600' as const,
    color: colors.text.primary,
  },
  body: {
    fontSize: 14,
    color: colors.text.secondary,
  },
  caption: {
    fontSize: 12,
    color: colors.text.muted,
  },
  small: {
    fontSize: 10,
    color: colors.text.subtle,
  },
} as const;

// Common shadow for cards
export const shadows = {
  card: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 3,
  },
} as const;
