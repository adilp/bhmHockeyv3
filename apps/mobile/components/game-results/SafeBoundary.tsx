import React from 'react';

interface Props {
  children: React.ReactNode;
  /** What to show instead when a child throws while rendering (default: nothing). */
  fallback?: React.ReactNode;
  /** Name for the console line, to find which section failed. */
  name?: string;
}

interface State {
  failed: boolean;
}

/**
 * Wraps every game-results section. If anything inside throws while rendering
 * (an unexpected server value the sanitizers missed, a bug), the section
 * disappears and the rest of the screen - the event, the roster, the
 * organization - keeps working. Nothing new can throw to the app's root.
 */
export class SafeBoundary extends React.Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    console.warn(`[game results] ${this.props.name ?? 'section'} hidden after a render error`, error);
  }

  render() {
    return this.state.failed ? (this.props.fallback ?? null) : this.props.children;
  }
}
