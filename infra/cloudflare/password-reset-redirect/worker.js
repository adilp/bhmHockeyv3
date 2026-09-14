/**
 * bhmhockey.com/reset - password reset redirect (Cloudflare Worker)
 *
 * Reset emails link to https://bhmhockey.com/reset?token=... rather than to
 * bhmhockey://..., because many email clients (Gmail especially) won't make a
 * custom-scheme link tappable. This page hands the token to the app via its
 * bhmhockey:// scheme, and falls back to telling the player to use the 6-digit
 * code from the same email.
 *
 * The token is a credential until it's used, so the page:
 *   - never logs it, and loads nothing from anywhere else (strict CSP)
 *   - sends no Referer, is never cached, and asks not to be indexed
 *   - only accepts a token shaped like one the API issues
 *
 * When the app later gets universal links (a store build), iOS will open the
 * app for this same URL before the page ever loads - no email or API change.
 */

const APP_RESET_URL = 'bhmhockey://reset-password';
const TOKEN_PATTERN = /^[A-Za-z0-9_-]{32,128}$/;

export default {
  async fetch(request) {
    const url = new URL(request.url);

    if (url.pathname !== '/reset' && url.pathname !== '/reset/') {
      return new Response('Not found', { status: 404, headers: headers('text/plain; charset=utf-8') });
    }

    const token = url.searchParams.get('token') || '';
    if (!TOKEN_PATTERN.test(token)) {
      return new Response(page(null), { status: 400, headers: headers('text/html; charset=utf-8') });
    }

    const appLink = `${APP_RESET_URL}?token=${encodeURIComponent(token)}`;
    return new Response(page(appLink), { status: 200, headers: headers('text/html; charset=utf-8') });
  },
};

function headers(contentType) {
  return {
    'Content-Type': contentType,
    'Cache-Control': 'no-store',
    'Referrer-Policy': 'no-referrer',
    'X-Robots-Tag': 'noindex, nofollow',
    'X-Content-Type-Options': 'nosniff',
    'Content-Security-Policy':
      "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; " +
      "base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
  };
}

function page(appLink) {
  const body = appLink
    ? `
      <h1>Reset your password</h1>
      <p>Opening the BHM Hockey app&hellip;</p>
      <p><a class="button" href="${appLink}">Open BHM Hockey</a></p>
      <p class="muted">Didn't open? Make sure BHM Hockey is installed and up to date, then enter the
      <strong>6-digit code</strong> from your email on the app's reset password screen.</p>
      <script>window.location.href = ${JSON.stringify(appLink).replace(/</g, '\\u003c')};</script>`
    : `
      <h1>This reset link isn't valid</h1>
      <p class="muted">It may be incomplete or already used. Open the most recent reset email and tap
      its link again, or enter the 6-digit code from that email in the app.</p>`;

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>BHM Hockey - Reset password</title>
  <style>
    body { margin: 0; min-height: 100vh; display: grid; place-items: center;
           background: #0D1117; color: #E6EDF3;
           font: 16px/1.5 -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
    main { max-width: 420px; padding: 32px 24px; text-align: center; }
    h1 { font-size: 24px; margin: 0 0 12px; }
    .button { display: inline-block; margin: 8px 0 16px; padding: 12px 24px; border-radius: 10px;
              background: #00D9C0; color: #0D1117; font-weight: 700; text-decoration: none; }
    .muted { color: #8B949E; font-size: 14px; }
  </style>
</head>
<body><main>${body}</main></body>
</html>`;
}
